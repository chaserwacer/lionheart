using System.Text.Json;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Ingestion;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;

namespace lionheart.Services.Ingestion;

public interface IIngestionParseService
{
    Task<Result<IngestionParseResponse>> ParseAsync(IdentityUser user, IngestionParseRequest request);
}

public class IngestionParseService : IIngestionParseService
{
    private readonly ChatClient _chatClient;
    private readonly ModelContext _context;

    public IngestionParseService(ChatClient chatClient, ModelContext context)
    {
        _chatClient = chatClient;
        _context = context;
    }

    public async Task<Result<IngestionParseResponse>> ParseAsync(IdentityUser user, IngestionParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return Result<IngestionParseResponse>.Invalid(new ValidationError
            {
                Identifier = nameof(request.RawText),
                ErrorMessage = "Input text cannot be empty."
            });
        }

        var userGuid = Guid.Parse(user.Id);

        // 1. Load user's existing entities for resolution
        var existingBases = await _context.MovementBases
            .Where(mb => mb.UserID == userGuid)
            .Select(mb => new { mb.MovementBaseID, mb.Name })
            .ToListAsync();

        var existingEquipment = await _context.Equipments
            .Where(e => e.UserID == userGuid)
            .Select(e => new { e.EquipmentID, e.Name })
            .ToListAsync();

        var existingModifiers = await _context.MovementModifiers
            .Where(mm => mm.UserID == userGuid)
            .Select(mm => new { mm.MovementModifierID, mm.Name })
            .ToListAsync();

        // 2. Build context for LLM — include existing entity names so it can match
        var existingContext = BuildExistingEntitiesContext(
            existingBases.Select(b => b.Name).ToList(),
            existingEquipment.Select(e => e.Name).ToList(),
            existingModifiers.Select(m => m.Name).ToList()
        );

        // 3. Call LLM to parse workout text
        LlmParseResult? llmResult;
        try
        {
            llmResult = await CallLlmParseAsync(request.RawText, existingContext);
        }
        catch (Exception ex)
        {
            return Result<IngestionParseResponse>.Error($"LLM parse failed: {ex.Message}");
        }

        if (llmResult == null || llmResult.Sessions == null)
        {
            return Result<IngestionParseResponse>.Error("LLM returned no parseable results.");
        }

        // 4. Build draft sessions with generated IDs
        var draftSessions = new List<DraftSession>();
        foreach (var llmSession in llmResult.Sessions)
        {
            var movements = new List<DraftMovement>();
            var ordering = 0;
            foreach (var llmMov in llmSession.Movements ?? [])
            {
                var liftSets = llmMov.LiftSets?.Select(s => new DraftLiftSet(
                    RecommendedReps: s.RecommendedReps,
                    RecommendedWeight: s.RecommendedWeight,
                    RecommendedRPE: s.RecommendedRPE,
                    ActualReps: s.ActualReps,
                    ActualWeight: s.ActualWeight,
                    ActualRPE: s.ActualRPE,
                    WeightUnit: ParseWeightUnit(s.WeightUnit)
                )).ToList();

                var dtSets = llmMov.DistanceTimeSets?.Select(s => new DraftDTSet(
                    ActualDistance: s.ActualDistance,
                    ActualDuration: TimeSpan.FromSeconds(s.ActualDurationSeconds),
                    ActualPace: TimeSpan.FromSeconds(s.ActualPaceSeconds),
                    IntervalType: ParseIntervalType(s.IntervalType),
                    DistanceUnit: ParseDistanceUnit(s.DistanceUnit),
                    ActualRPE: s.ActualRPE
                )).ToList();

                movements.Add(new DraftMovement(
                    DraftId: Guid.NewGuid().ToString(),
                    MovementBaseName: llmMov.MovementBaseName ?? "",
                    EquipmentName: llmMov.EquipmentName ?? "Bodyweight",
                    ModifierName: string.IsNullOrWhiteSpace(llmMov.ModifierName) ? null : llmMov.ModifierName,
                    LiftSets: liftSets,
                    DistanceTimeSets: dtSets,
                    Notes: llmMov.Notes ?? "",
                    Ordering: ordering++
                ));
            }

            draftSessions.Add(new DraftSession(
                DraftId: Guid.NewGuid().ToString(),
                Date: llmSession.Date != default ? llmSession.Date : DateTime.Today,
                Notes: llmSession.Notes ?? "",
                Movements: movements
            ));
        }

        // 5. Resolve entity references
        var unresolvedReferences = new List<UnresolvedReference>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in draftSessions)
        {
            foreach (var movement in session.Movements)
            {
                // Resolve MovementBase
                var baseKey = $"MovementBase:{movement.MovementBaseName}";
                if (!seenNames.Contains(baseKey))
                {
                    seenNames.Add(baseKey);
                    var exactBase = existingBases.FirstOrDefault(b =>
                        b.Name.Equals(movement.MovementBaseName, StringComparison.OrdinalIgnoreCase));

                    if (exactBase == null)
                    {
                        var candidates = FindFuzzyCandidates(
                            movement.MovementBaseName,
                            existingBases.Select(b => (b.MovementBaseID, b.Name)).ToList()
                        );
                        unresolvedReferences.Add(new UnresolvedReference(
                            RawName: movement.MovementBaseName,
                            EntityType: "MovementBase",
                            Candidates: candidates
                        ));
                    }
                }

                // Resolve Equipment
                var equipKey = $"Equipment:{movement.EquipmentName}";
                if (!seenNames.Contains(equipKey))
                {
                    seenNames.Add(equipKey);
                    var exactEquip = existingEquipment.FirstOrDefault(e =>
                        e.Name.Equals(movement.EquipmentName, StringComparison.OrdinalIgnoreCase));

                    if (exactEquip == null)
                    {
                        var candidates = FindFuzzyCandidates(
                            movement.EquipmentName,
                            existingEquipment.Select(e => (e.EquipmentID, e.Name)).ToList()
                        );
                        unresolvedReferences.Add(new UnresolvedReference(
                            RawName: movement.EquipmentName,
                            EntityType: "Equipment",
                            Candidates: candidates
                        ));
                    }
                }

                // Resolve Modifier (modifiers use FindOrCreate pattern, so only flag if ambiguous)
                if (!string.IsNullOrWhiteSpace(movement.ModifierName))
                {
                    var modKey = $"MovementModifier:{movement.ModifierName}";
                    if (!seenNames.Contains(modKey))
                    {
                        seenNames.Add(modKey);
                        var exactMod = existingModifiers.FirstOrDefault(m =>
                            m.Name.Equals(movement.ModifierName.Trim(), StringComparison.OrdinalIgnoreCase));

                        if (exactMod == null)
                        {
                            var candidates = FindFuzzyCandidates(
                                movement.ModifierName,
                                existingModifiers.Select(m => (m.MovementModifierID, m.Name)).ToList()
                            );
                            // Modifiers are auto-created, so only flag if there are close candidates
                            // that might indicate the user meant an existing one
                            if (candidates.Count > 0)
                            {
                                unresolvedReferences.Add(new UnresolvedReference(
                                    RawName: movement.ModifierName,
                                    EntityType: "MovementModifier",
                                    Candidates: candidates
                                ));
                            }
                        }
                    }
                }
            }
        }

        return Result<IngestionParseResponse>.Success(new IngestionParseResponse(
            Sessions: draftSessions,
            UnresolvedReferences: unresolvedReferences,
            Warnings: llmResult.Warnings ?? []
        ));
    }

    private async Task<LlmParseResult?> CallLlmParseAsync(string rawText, string existingContext)
    {
        var systemPrompt = BuildSystemPrompt(existingContext);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(rawText)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "training_parse_result",
                jsonSchema: BinaryData.FromString(LlmParseResultSchema),
                jsonSchemaIsStrict: true
            ),
            Temperature = 0.1f
        };

        var completion = await _chatClient.CompleteChatAsync(messages, options);

        if (completion.Value.FinishReason == ChatFinishReason.Stop)
        {
            var json = completion.Value.Content[0].Text;
            return JsonSerializer.Deserialize<LlmParseResult>(json, _jsonOptions);
        }

        throw new InvalidOperationException($"LLM finished with reason: {completion.Value.FinishReason}");
    }

    private string BuildExistingEntitiesContext(
        List<string> bases, List<string> equipment, List<string> modifiers)
    {
        var parts = new List<string>();
        if (bases.Count > 0) parts.Add($"Existing movement bases: {string.Join(", ", bases)}");
        if (equipment.Count > 0) parts.Add($"Existing equipment: {string.Join(", ", equipment)}");
        if (modifiers.Count > 0) parts.Add($"Existing modifiers: {string.Join(", ", modifiers)}");
        return string.Join("\n", parts);
    }

    private static string BuildSystemPrompt(string existingContext)
    {
        return $"""
            You are a training log parser. Your job is to extract structured workout data from freeform text.

            RULES:
            - Extract each training session with its date, movements, sets, and notes.
            - If no date is specified, use today's date.
            - Each movement has a movement base name (the exercise, e.g. "Squat", "Bench Press", "Running").
            - Each movement has equipment (e.g. "Barbell", "Dumbbell", "Cable Machine"). Default to "Bodyweight" if not specified.
            - Each movement may have a modifier (e.g. "Incline", "Paused", "Tempo", "Close Grip"). Only include if explicitly stated.
            - For lifting movements, extract sets with reps, weight, RPE. If weight unit not specified, default to "Pounds".
            - For distance/time movements (running, swimming, rowing), extract distance, duration, pace.
            - Ignore rest timer rows, warmup notes, or general commentary. Add these to warnings if notable.
            - IMPORTANT: Use the existing entity names below when they match what's described. Prefer exact matches.
            - If the text describes multiple sessions (e.g. different dates), create separate sessions.
            - Put any unparseable or ambiguous content in the warnings array.

            {existingContext}

            Respond with valid JSON matching the provided schema.
            """;
    }

    private static List<EntityMatch> FindFuzzyCandidates(
        string rawName, List<(Guid Id, string Name)> existing)
    {
        var candidates = new List<EntityMatch>();
        var normalizedRaw = rawName.Trim().ToLowerInvariant();

        foreach (var (id, name) in existing)
        {
            var normalizedExisting = name.Trim().ToLowerInvariant();
            var confidence = CalculateSimilarity(normalizedRaw, normalizedExisting);
            if (confidence >= 0.4)
            {
                candidates.Add(new EntityMatch(id, name, Math.Round(confidence, 2)));
            }
        }

        return candidates.OrderByDescending(c => c.Confidence).Take(5).ToList();
    }

    private static double CalculateSimilarity(string a, string b)
    {
        // Containment check — if one string contains the other
        if (a.Contains(b) || b.Contains(a))
        {
            var shorter = Math.Min(a.Length, b.Length);
            var longer = Math.Max(a.Length, b.Length);
            return (double)shorter / longer;
        }

        // Levenshtein distance normalized to similarity
        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        return 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }
        return d[n, m];
    }

    private static WeightUnit ParseWeightUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return WeightUnit.Pounds;
        return unit.Trim().ToLowerInvariant() switch
        {
            "kg" or "kilograms" or "kgs" => WeightUnit.Kilograms,
            _ => WeightUnit.Pounds
        };
    }

    private static IntervalType ParseIntervalType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return IntervalType.ContinuousDistance;
        return Enum.TryParse<IntervalType>(type, ignoreCase: true, out var result)
            ? result
            : IntervalType.ContinuousDistance;
    }

    private static DistanceUnit ParseDistanceUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return DistanceUnit.Miles;
        return unit.Trim().ToLowerInvariant() switch
        {
            "m" or "meters" => DistanceUnit.Meters,
            "yd" or "yards" => DistanceUnit.Yards,
            "km" or "kilometers" => DistanceUnit.Kilometers,
            _ => DistanceUnit.Miles
        };
    }

    // ─────────────── LLM Response Types ───────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private class LlmParseResult
    {
        public List<LlmSession>? Sessions { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private class LlmSession
    {
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
        public List<LlmMovement>? Movements { get; set; }
    }

    private class LlmMovement
    {
        public string? MovementBaseName { get; set; }
        public string? EquipmentName { get; set; }
        public string? ModifierName { get; set; }
        public List<LlmLiftSet>? LiftSets { get; set; }
        public List<LlmDTSet>? DistanceTimeSets { get; set; }
        public string? Notes { get; set; }
    }

    private class LlmLiftSet
    {
        public int? RecommendedReps { get; set; }
        public double? RecommendedWeight { get; set; }
        public double? RecommendedRPE { get; set; }
        public int ActualReps { get; set; }
        public double ActualWeight { get; set; }
        public double ActualRPE { get; set; }
        public string? WeightUnit { get; set; }
    }

    private class LlmDTSet
    {
        public double ActualDistance { get; set; }
        public double ActualDurationSeconds { get; set; }
        public double ActualPaceSeconds { get; set; }
        public string? IntervalType { get; set; }
        public string? DistanceUnit { get; set; }
        public double ActualRPE { get; set; }
    }

    // ─────────────── JSON Schema for Structured Output ───────────────

    private const string LlmParseResultSchema = """
    {
        "type": "object",
        "properties": {
            "sessions": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "date": { "type": "string", "description": "ISO 8601 date string (yyyy-MM-dd)" },
                        "notes": { "type": "string" },
                        "movements": {
                            "type": "array",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "movementBaseName": { "type": "string", "description": "The exercise name (e.g. Squat, Bench Press, Running)" },
                                    "equipmentName": { "type": "string", "description": "Equipment used (e.g. Barbell, Dumbbell, Bodyweight)" },
                                    "modifierName": { "type": ["string", "null"], "description": "Movement variation if any (e.g. Incline, Paused, Close Grip)" },
                                    "liftSets": {
                                        "type": ["array", "null"],
                                        "items": {
                                            "type": "object",
                                            "properties": {
                                                "recommendedReps": { "type": ["integer", "null"] },
                                                "recommendedWeight": { "type": ["number", "null"] },
                                                "recommendedRPE": { "type": ["number", "null"] },
                                                "actualReps": { "type": "integer" },
                                                "actualWeight": { "type": "number" },
                                                "actualRPE": { "type": "number", "description": "Rate of perceived exertion 1-10. Default to 0 if not specified." },
                                                "weightUnit": { "type": "string", "description": "Pounds or Kilograms. Default Pounds." }
                                            },
                                            "required": ["recommendedReps", "recommendedWeight", "recommendedRPE", "actualReps", "actualWeight", "actualRPE", "weightUnit"],
                                            "additionalProperties": false
                                        }
                                    },
                                    "distanceTimeSets": {
                                        "type": ["array", "null"],
                                        "items": {
                                            "type": "object",
                                            "properties": {
                                                "actualDistance": { "type": "number" },
                                                "actualDurationSeconds": { "type": "number", "description": "Duration in seconds" },
                                                "actualPaceSeconds": { "type": "number", "description": "Pace in seconds per unit" },
                                                "intervalType": { "type": "string", "description": "One of: ContinuousDistance, ContinuousTime, ContinuousDistanceAndTime, RepetitionDistance, RepetitionTime, RepetitionDistanceAndTime, IntervalDistance, IntervalTime, IntervalDistanceAndTime" },
                                                "distanceUnit": { "type": "string", "description": "One of: Meters, Yards, Miles, Kilometers" },
                                                "actualRPE": { "type": "number" }
                                            },
                                            "required": ["actualDistance", "actualDurationSeconds", "actualPaceSeconds", "intervalType", "distanceUnit", "actualRPE"],
                                            "additionalProperties": false
                                        }
                                    },
                                    "notes": { "type": "string" }
                                },
                                "required": ["movementBaseName", "equipmentName", "modifierName", "liftSets", "distanceTimeSets", "notes"],
                                "additionalProperties": false
                            }
                        }
                    },
                    "required": ["date", "notes", "movements"],
                    "additionalProperties": false
                }
            },
            "warnings": {
                "type": "array",
                "items": { "type": "string" }
            }
        },
        "required": ["sessions", "warnings"],
        "additionalProperties": false
    }
    """;
}
