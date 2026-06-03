using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Ingestion;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using lionheart.Services.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Ingestion;

public interface IIngestionCommitService
{
    Task<Result<IngestionCommitResponse>> CommitAsync(IdentityUser user, IngestionCommitRequest request);
}

public class IngestionCommitService : IIngestionCommitService
{
    private readonly ModelContext _context;
    private readonly IEquipmentService _equipmentService;
    private readonly IMovementBaseService _movementBaseService;
    private readonly IMovementDataService _movementDataService;
    private readonly IMovementModifierService _movementModifierService;

    public IngestionCommitService(
        ModelContext context,
        IEquipmentService equipmentService,
        IMovementBaseService movementBaseService,
        IMovementDataService movementDataService,
        IMovementModifierService movementModifierService)
    {
        _context = context;
        _equipmentService = equipmentService;
        _movementBaseService = movementBaseService;
        _movementDataService = movementDataService;
        _movementModifierService = movementModifierService;
    }

    public async Task<Result<IngestionCommitResponse>> CommitAsync(IdentityUser user, IngestionCommitRequest request)
    {
        if (request.Sessions == null || request.Sessions.Count == 0)
        {
            return Result<IngestionCommitResponse>.Invalid(new ValidationError
            {
                Identifier = nameof(request.Sessions),
                ErrorMessage = "No sessions to commit."
            });
        }

        var userGuid = Guid.Parse(user.Id);
        var createdSessionIDs = new List<Guid>();
        var createdDependencies = new List<string>();
        var errors = new List<string>();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // ── Phase 1: Create missing Equipment (deduplicated) ──
            var equipmentNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            // Pre-load existing equipment
            var existingEquipment = await _context.Equipments
                .Where(e => e.UserID == userGuid)
                .ToListAsync();
            foreach (var eq in existingEquipment)
            {
                equipmentNameToId.TryAdd(eq.Name, eq.EquipmentID);
            }

            // Find all unique new equipment names
            var newEquipmentNames = request.Sessions
                .SelectMany(s => s.Movements)
                .Where(m => m.EquipmentID == null && !string.IsNullOrWhiteSpace(m.NewEquipmentName))
                .Select(m => m.NewEquipmentName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !equipmentNameToId.ContainsKey(name))
                .ToList();

            foreach (var name in newEquipmentNames)
            {
                var result = await _equipmentService.CreateEquipmentAsync(user, new CreateEquipmentRequest(name));
                if (result.IsSuccess)
                {
                    equipmentNameToId[name] = result.Value.EquipmentID;
                    createdDependencies.Add($"Created equipment: {name}");
                }
                else
                {
                    // May already exist (race condition or case mismatch) — try to find it
                    var existing = existingEquipment.FirstOrDefault(e =>
                        e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        equipmentNameToId[name] = existing.EquipmentID;
                    }
                    else
                    {
                        errors.Add($"Failed to create equipment '{name}': {string.Join(", ", result.Errors)}");
                    }
                }
            }

            // ── Phase 2: Create missing MovementBases (deduplicated) ──
            var baseNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            var existingBases = await _context.MovementBases
                .Where(mb => mb.UserID == userGuid)
                .ToListAsync();
            foreach (var mb in existingBases)
            {
                baseNameToId.TryAdd(mb.Name, mb.MovementBaseID);
            }

            var newBaseNames = request.Sessions
                .SelectMany(s => s.Movements)
                .Where(m => m.MovementBaseID == null && !string.IsNullOrWhiteSpace(m.NewMovementBaseName))
                .Select(m => m.NewMovementBaseName!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !baseNameToId.ContainsKey(name))
                .ToList();

            foreach (var name in newBaseNames)
            {
                var result = await _movementBaseService.CreateMovementBaseAsync(user,
                    new CreateMovementBaseRequest(name, "", new List<MuscleGroup>()));
                if (result.IsSuccess)
                {
                    baseNameToId[name] = result.Value.MovementBaseID;
                    createdDependencies.Add($"Created movement base: {name}");
                }
                else
                {
                    var existing = existingBases.FirstOrDefault(b =>
                        b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        baseNameToId[name] = existing.MovementBaseID;
                    }
                    else
                    {
                        errors.Add($"Failed to create movement base '{name}': {string.Join(", ", result.Errors)}");
                    }
                }
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync();
                return Result<IngestionCommitResponse>.Error(string.Join("; ", errors));
            }

            // ── Phase 3: Create Sessions, Movements, and Sets ──
            foreach (var resolvedSession in request.Sessions)
            {
                var session = new TrainingSession
                {
                    TrainingSessionID = Guid.NewGuid(),
                    UserID = userGuid,
                    Date = DateOnly.FromDateTime(resolvedSession.Date),
                    Status = TrainingSessionStatus.Planned,
                    CreationTime = DateTime.UtcNow,
                    Notes = resolvedSession.Notes ?? string.Empty,
                    TrainingProgramID = resolvedSession.TrainingProgramID,
                    Movements = new List<Movement>()
                };

                _context.TrainingSessions.Add(session);
                await _context.SaveChangesAsync();

                foreach (var resolvedMovement in resolvedSession.Movements)
                {
                    // Resolve IDs
                    var equipmentId = resolvedMovement.EquipmentID
                        ?? (resolvedMovement.NewEquipmentName != null && equipmentNameToId.TryGetValue(resolvedMovement.NewEquipmentName, out var eqId) ? eqId : (Guid?)null);
                    var movementBaseId = resolvedMovement.MovementBaseID
                        ?? (resolvedMovement.NewMovementBaseName != null && baseNameToId.TryGetValue(resolvedMovement.NewMovementBaseName, out var mbId) ? mbId : (Guid?)null);

                    if (equipmentId == null || movementBaseId == null)
                    {
                        errors.Add($"Could not resolve equipment or movement base for movement.");
                        continue;
                    }

                    // FindOrCreate MovementData (handles modifier via name)
                    var movementDataResult = await _movementDataService.FindOrCreateMovementDataAsync(user,
                        new CreateMovementDataRequest(
                            EquipmentID: equipmentId.Value,
                            MovementBaseID: movementBaseId.Value,
                            MovementModifierName: resolvedMovement.ModifierName
                        ));

                    if (!movementDataResult.IsSuccess)
                    {
                        errors.Add($"Failed to create movement data: {string.Join(", ", movementDataResult.Errors)}");
                        continue;
                    }

                    var movement = new Movement
                    {
                        MovementID = Guid.NewGuid(),
                        TrainingSessionID = session.TrainingSessionID,
                        MovementDataID = movementDataResult.Value.MovementDataID,
                        Notes = resolvedMovement.Notes ?? string.Empty,
                        IsCompleted = false,
                        Ordering = resolvedMovement.Ordering,
                        LiftSets = new List<LiftSetEntry>(),
                        DistanceTimeSets = new List<DTSetEntry>()
                    };

                    _context.Movements.Add(movement);
                    await _context.SaveChangesAsync();

                    // Create lift sets
                    if (resolvedMovement.LiftSets != null)
                    {
                        foreach (var draftSet in resolvedMovement.LiftSets)
                        {
                            var setEntry = new LiftSetEntry
                            {
                                SetEntryID = Guid.NewGuid(),
                                MovementID = movement.MovementID,
                                Movement = movement,
                                RecommendedReps = draftSet.RecommendedReps,
                                RecommendedWeight = draftSet.RecommendedWeight,
                                RecommendedRPE = draftSet.RecommendedRPE,
                                ActualReps = draftSet.ActualReps,
                                ActualWeight = draftSet.ActualWeight,
                                ActualRPE = draftSet.ActualRPE,
                                WeightUnit = draftSet.WeightUnit
                            };
                            _context.LiftSetEntries.Add(setEntry);
                        }
                    }

                    // Create distance/time sets
                    if (resolvedMovement.DistanceTimeSets != null)
                    {
                        foreach (var draftSet in resolvedMovement.DistanceTimeSets)
                        {
                            var setEntry = new DTSetEntry
                            {
                                SetEntryID = Guid.NewGuid(),
                                MovementID = movement.MovementID,
                                Movement = movement,
                                RecommendedDistance = 0,
                                ActualDistance = draftSet.ActualDistance,
                                IntervalDuration = TimeSpan.Zero,
                                TargetPace = TimeSpan.Zero,
                                ActualPace = draftSet.ActualPace,
                                RecommendedDuration = TimeSpan.Zero,
                                ActualDuration = draftSet.ActualDuration,
                                RecommendedRest = TimeSpan.Zero,
                                ActualRest = TimeSpan.Zero,
                                IntervalType = draftSet.IntervalType,
                                DistanceUnit = draftSet.DistanceUnit,
                                ActualRPE = draftSet.ActualRPE
                            };
                            _context.DTSetEntries.Add(setEntry);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                createdSessionIDs.Add(session.TrainingSessionID);
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync();
                return Result<IngestionCommitResponse>.Error(string.Join("; ", errors));
            }

            await transaction.CommitAsync();

            return Result<IngestionCommitResponse>.Created(new IngestionCommitResponse(
                CreatedSessionIDs: createdSessionIDs,
                CreatedDependencies: createdDependencies,
                Errors: errors
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result<IngestionCommitResponse>.Error($"Commit failed: {ex.Message}");
        }
    }
}
