using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training.SetEntry;

namespace lionheart.Model.Ingestion;

// ─────────────── Parse Request / Response ───────────────

public record IngestionParseRequest(
    [Required] string RawText,
    string? SourceDescription
);

public record IngestionParseResponse(
    List<DraftSession> Sessions,
    List<UnresolvedReference> UnresolvedReferences,
    List<string> Warnings
);

// ─────────────── Draft Objects (from LLM parse) ───────────────

public record DraftSession(
    string DraftId,
    DateTime Date,
    string Notes,
    List<DraftMovement> Movements
);

public record DraftMovement(
    string DraftId,
    string MovementBaseName,
    string EquipmentName,
    string? ModifierName,
    List<DraftLiftSet>? LiftSets,
    List<DraftDTSet>? DistanceTimeSets,
    string Notes,
    int Ordering
);

public record DraftLiftSet(
    int? RecommendedReps,
    double? RecommendedWeight,
    double? RecommendedRPE,
    int ActualReps,
    double ActualWeight,
    double ActualRPE,
    WeightUnit WeightUnit
);

public record DraftDTSet(
    double ActualDistance,
    TimeSpan ActualDuration,
    TimeSpan ActualPace,
    IntervalType IntervalType,
    DistanceUnit DistanceUnit,
    double ActualRPE
);

// ─────────────── Entity Resolution ───────────────

public record UnresolvedReference(
    string RawName,
    string EntityType,
    List<EntityMatch> Candidates
);

public record EntityMatch(
    Guid EntityId,
    string Name,
    double Confidence
);

// ─────────────── Commit Request / Response ───────────────

public record IngestionCommitRequest(
    [Required] List<ResolvedSession> Sessions
);

public record ResolvedSession(
    DateTime Date,
    string Notes,
    Guid? TrainingProgramID,
    List<ResolvedMovement> Movements
);

public record ResolvedMovement(
    Guid? MovementBaseID,
    string? NewMovementBaseName,
    Guid? EquipmentID,
    string? NewEquipmentName,
    string? ModifierName,
    List<DraftLiftSet>? LiftSets,
    List<DraftDTSet>? DistanceTimeSets,
    string Notes,
    int Ordering
);

public record IngestionCommitResponse(
    List<Guid> CreatedSessionIDs,
    List<string> CreatedDependencies,
    List<string> Errors
);
