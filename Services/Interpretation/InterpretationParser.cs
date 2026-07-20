using System.Text.Json;
using Ardalis.Result;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using lionheart.Services.Chat;
using OpenAI.Chat;

namespace lionheart.Services.Interpretation;

public interface IInterpretationParser
{
    Task<Result<ParsedInterpretation>> ParseAsync(InterpretationRequest request, string knownMovementContext);
}

/// <summary>
/// Stage one of the engine: raw text becomes drafts plus a proposed decoration. The parser
/// never invents structure it cannot support — an utterance it can't break down stays a
/// <see cref="DraftFreeformSet"/> rather than becoming a badly guessed lift.
/// </summary>
public sealed class InterpretationParser : IInterpretationParser
{
    private readonly ChatClient _chatClient;

    public InterpretationParser(ChatClient chatClient) => _chatClient = chatClient;

    public async Task<Result<ParsedInterpretation>> ParseAsync(
        InterpretationRequest request, string knownMovementContext)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return Result<ParsedInterpretation>.Invalid(new ValidationError
            {
                Identifier = nameof(request.RawText),
                ErrorMessage = "Input text cannot be empty."
            });
        }

        var completion = await RequestParseAsync(request, knownMovementContext);
        if (!completion.IsSuccess) return Result<ParsedInterpretation>.Error(
            new ErrorList([.. completion.Errors]));

        return MapToDrafts(completion.Value, request.LoggedAt);
    }

    private async Task<Result<LlmParse>> RequestParseAsync(
        InterpretationRequest request, string knownMovementContext)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt(request.LoggedAt, knownMovementContext)),
            new UserChatMessage(request.RawText)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "interpretation",
                jsonSchema: BinaryData.FromString(ParseSchema),
                jsonSchemaIsStrict: true),
            Temperature = 0.1f
        };

        var completionResult = (await _chatClient.CompleteChatAsync(messages, options)).ToResult();
        if (completionResult.IsError()) return Result<LlmParse>.Error(
            new ErrorList([.. completionResult.Errors]));

        var completion = completionResult.Value;
        if (completion.FinishReason != ChatFinishReason.Stop)
        {
            return Result<LlmParse>.Error($"Parse stopped early: {completion.FinishReason}.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LlmParse>(completion.Content[0].Text, JsonOptions);
            return parsed is null
                ? Result<LlmParse>.Error("Parser returned no interpretable content.")
                : Result<LlmParse>.Success(parsed);
        }
        catch (JsonException ex)
        {
            return Result<LlmParse>.Error($"Parser returned malformed JSON: {ex.Message}");
        }
    }

    private static Result<ParsedInterpretation> MapToDrafts(LlmParse parse, DateTime loggedAt)
    {
        var entries = new List<DraftEntry>();
        foreach (var entry in parse.Entries ?? [])
        {
            var mapped = MapEntry(entry, loggedAt);
            if (!mapped.IsSuccess) return Result<ParsedInterpretation>.Error(
                new ErrorList([.. mapped.Errors]));
            entries.Add(mapped.Value);
        }

        return Result<ParsedInterpretation>.Success(
            new ParsedInterpretation(entries, parse.Warnings ?? []));
    }

    private static Result<DraftEntry> MapEntry(LlmEntry entry, DateTime loggedAt)
    {
        var period = MapPeriod(entry, loggedAt);
        if (!period.IsSuccess) return Result<DraftEntry>.Error(new ErrorList([.. period.Errors]));

        var decoration = MapDecoration(entry.Decoration);
        var notes = entry.Notes ?? string.Empty;

        // Perceived-effort ratings are a numeric self-report from the UI, never something
        // to guess from prose — text sentiment belongs in the decoration's valence instead.
        DraftEntry draft = entry.Kind switch
        {
            "session" => new DraftSession([.. (entry.Sets ?? []).Select(MapSet)], new EffortNotReported())
            {
                Period = period.Value,
                Notes = notes,
                Decoration = decoration
            },
            "exertionContext" => new DraftExertionContext(new EffortNotReported())
            {
                Period = period.Value,
                Notes = notes,
                Decoration = decoration
            },
            _ => new DraftStateContext
            {
                Period = period.Value,
                Notes = notes,
                Decoration = decoration
            }
        };

        return Result<DraftEntry>.Success(draft);
    }

    private static Result<Period> MapPeriod(LlmEntry entry, DateTime loggedAt)
    {
        var start = entry.Start ?? loggedAt;
        if (entry.End is not { } end) return Result<Period>.Success(new Instant(start));

        return end < start
            ? Result<Period>.Error($"Parsed period ends ({end:o}) before it starts ({start:o}).")
            : Result<Period>.Success(new Interval(start, end));
    }

    private static ProposedDecoration MapDecoration(LlmDecoration? decoration) =>
        new(
            Facets: [.. (decoration?.Facets ?? []).Select(MapFacet).OfType<ProposedFacet>()],
            FreeTags: decoration?.FreeTags ?? [],
            Valence: Enum.TryParse<Valence>(decoration?.Valence, ignoreCase: true, out var valence)
                ? valence
                : Valence.Neutral,
            SummaryText: decoration?.SummaryText ?? string.Empty);

    private static ProposedFacet? MapFacet(LlmFacet facet) =>
        Enum.TryParse<FacetAxis>(facet.Axis, ignoreCase: true, out var axis)
            && !string.IsNullOrWhiteSpace(facet.Value)
                ? new ProposedFacet(axis, facet.Value.Trim())
                : null; // an axis outside the closed set is a parser miss, not a new axis

    private static DraftSet MapSet(LlmSet set)
    {
        var source = new Texted(set.RawText ?? string.Empty, Math.Clamp(set.StructureConfidence, 0, 1));
        IReadOnlyList<ProposedFacet> facets = [.. (set.Facets ?? []).Select(MapFacet).OfType<ProposedFacet>()];
        var rpe = set.Rpe is { } value && value is >= 0 and <= 10 ? Rpe.Of(value) : Rpe.Unrated;
        var movement = new MovementReference(
            BaseName: set.MovementBaseName?.Trim() ?? string.Empty,
            EquipmentName: string.IsNullOrWhiteSpace(set.EquipmentName) ? DefaultEquipment : set.EquipmentName.Trim(),
            ModifierName: set.ModifierName?.Trim() ?? string.Empty);

        return set.Kind switch
        {
            "lift" => new DraftLiftSet(movement, set.Reps ?? 0, set.Weight ?? 0, MapWeightUnit(set.WeightUnit), rpe)
            {
                Source = source,
                Facets = facets
            },
            "distanceTime" => new DraftDistanceTimeSet(
                movement, set.Distance ?? 0, MapDistanceUnit(set.DistanceUnit), MapDuration(set.DurationSeconds), rpe)
            {
                Source = source,
                Facets = facets
            },
            "trailRun" => new DraftTrailRunSet(
                set.Distance ?? 0, MapDistanceUnit(set.DistanceUnit), MapDuration(set.DurationSeconds),
                set.ElevationGainMeters ?? 0, rpe)
            {
                Source = source,
                Facets = facets
            },
            _ => new DraftFreeformSet { Source = source, Facets = facets }
        };
    }

    private static TimeSpan MapDuration(double? seconds) =>
        seconds is { } value && value > 0 ? TimeSpan.FromSeconds(value) : TimeSpan.Zero;

    private static WeightUnit MapWeightUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "kg" or "kgs" or "kilograms" => WeightUnit.Kilograms,
        _ => WeightUnit.Pounds
    };

    private static DistanceUnit MapDistanceUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "m" or "meters" => DistanceUnit.Meters,
        "yd" or "yards" => DistanceUnit.Yards,
        "km" or "kilometers" => DistanceUnit.Kilometers,
        _ => DistanceUnit.Miles
    };

    private const string DefaultEquipment = "Bodyweight";

    private static string BuildSystemPrompt(DateTime loggedAt, string knownMovementContext) =>
        $$"""
        You interpret an athlete's training log. Extract what is actually there and nothing more.

        ENTRIES — the text may describe several. Each is one of:
        - "session": deliberate training. Carries sets.
        - "exertionContext": physical exertion that is not training (pressure-washing, moving furniture).
        - "stateContext": a body or life state with no exertion (bad sleep, exam stress, illness).

        TIME — "start" is when it happened; add "end" only when a span is stated or clearly
        implied ("exam week"). Default to {{loggedAt:yyyy-MM-dd}} when no date is given.

        SETS — one object per performed set. Pick the smallest shape that fits:
        - "lift": reps and weight are both known.
        - "distanceTime": a distance and a duration are known.
        - "trailRun": a run with elevation gain.
        - "freeform": ANY set you cannot confidently break down. This is a correct answer,
          not a failure — never invent reps, weights, or distances to fill a richer shape.
        Always set "rawText" to the fragment the set came from, and "structureConfidence"
        to how sure you are of the structure you lifted (hedged input like "2 or 3 sets"
        should score low). Use null for "rpe" unless effort was actually rated.

        DECORATION — annotate every entry:
        - "facets": (axis, value) pairs drawn ONLY from these axes:
          Modality (lifting, running, mobility...), MovementPattern (squat, hinge, push, pull...),
          MuscleRegion (quads, posterior chain...), Intent (strength, endurance, recovery...),
          LifeDomain (work, family, travel...), BodyState (fatigued, sore, energized...).
          Prefer plain, reusable single words or short phrases — these become shared vocabulary.
        - "freeTags": open labels worth recalling later.
        - "valence": how the entry felt overall.
        - "summaryText": one sentence, written to be searched later.
        Also put facets on individual sets where they differ from the entry as a whole.

        Put anything you could not interpret into "warnings" rather than guessing.

        {{knownMovementContext}}
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // The LLM-boundary shapes are deliberately flat and nullable: strict JSON schema has no
    // sum types, so discrimination happens on a "kind" string. The flatness stops here —
    // MapSet/MapEntry translate straight into the sealed domain shapes.
    private sealed class LlmParse
    {
        public List<LlmEntry>? Entries { get; set; }
        public List<string>? Warnings { get; set; }
    }

    private sealed class LlmEntry
    {
        public string? Kind { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string? Notes { get; set; }
        public LlmDecoration? Decoration { get; set; }
        public List<LlmSet>? Sets { get; set; }
    }

    private sealed class LlmDecoration
    {
        public List<LlmFacet>? Facets { get; set; }
        public List<string>? FreeTags { get; set; }
        public string? Valence { get; set; }
        public string? SummaryText { get; set; }
    }

    private sealed class LlmFacet
    {
        public string? Axis { get; set; }
        public string? Value { get; set; }
    }

    private sealed class LlmSet
    {
        public string? Kind { get; set; }
        public string? RawText { get; set; }
        public double StructureConfidence { get; set; }
        public List<LlmFacet>? Facets { get; set; }
        public string? MovementBaseName { get; set; }
        public string? EquipmentName { get; set; }
        public string? ModifierName { get; set; }
        public int? Reps { get; set; }
        public double? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public double? Distance { get; set; }
        public string? DistanceUnit { get; set; }
        public double? DurationSeconds { get; set; }
        public double? ElevationGainMeters { get; set; }
        public double? Rpe { get; set; }
    }

    private const string ParseSchema = """
    {
        "type": "object",
        "properties": {
            "entries": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "kind": { "type": "string", "enum": ["session", "exertionContext", "stateContext"] },
                        "start": { "type": ["string", "null"], "description": "ISO 8601 date-time" },
                        "end": { "type": ["string", "null"], "description": "ISO 8601 date-time; null unless a span" },
                        "notes": { "type": "string" },
                        "decoration": {
                            "type": "object",
                            "properties": {
                                "facets": {
                                    "type": "array",
                                    "items": {
                                        "type": "object",
                                        "properties": {
                                            "axis": { "type": "string", "enum": ["Modality", "MovementPattern", "MuscleRegion", "Intent", "LifeDomain", "BodyState"] },
                                            "value": { "type": "string" }
                                        },
                                        "required": ["axis", "value"],
                                        "additionalProperties": false
                                    }
                                },
                                "freeTags": { "type": "array", "items": { "type": "string" } },
                                "valence": { "type": "string", "enum": ["Positive", "Negative", "Neutral", "Mixed"] },
                                "summaryText": { "type": "string" }
                            },
                            "required": ["facets", "freeTags", "valence", "summaryText"],
                            "additionalProperties": false
                        },
                        "sets": {
                            "type": "array",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "kind": { "type": "string", "enum": ["lift", "distanceTime", "trailRun", "freeform"] },
                                    "rawText": { "type": "string" },
                                    "structureConfidence": { "type": "number" },
                                    "facets": {
                                        "type": "array",
                                        "items": {
                                            "type": "object",
                                            "properties": {
                                                "axis": { "type": "string", "enum": ["Modality", "MovementPattern", "MuscleRegion", "Intent", "LifeDomain", "BodyState"] },
                                                "value": { "type": "string" }
                                            },
                                            "required": ["axis", "value"],
                                            "additionalProperties": false
                                        }
                                    },
                                    "movementBaseName": { "type": ["string", "null"] },
                                    "equipmentName": { "type": ["string", "null"] },
                                    "modifierName": { "type": ["string", "null"] },
                                    "reps": { "type": ["integer", "null"] },
                                    "weight": { "type": ["number", "null"] },
                                    "weightUnit": { "type": ["string", "null"] },
                                    "distance": { "type": ["number", "null"] },
                                    "distanceUnit": { "type": ["string", "null"] },
                                    "durationSeconds": { "type": ["number", "null"] },
                                    "elevationGainMeters": { "type": ["number", "null"] },
                                    "rpe": { "type": ["number", "null"] }
                                },
                                "required": ["kind", "rawText", "structureConfidence", "facets", "movementBaseName", "equipmentName", "modifierName", "reps", "weight", "weightUnit", "distance", "distanceUnit", "durationSeconds", "elevationGainMeters", "rpe"],
                                "additionalProperties": false
                            }
                        }
                    },
                    "required": ["kind", "start", "end", "notes", "decoration", "sets"],
                    "additionalProperties": false
                }
            },
            "warnings": { "type": "array", "items": { "type": "string" } }
        },
        "required": ["entries", "warnings"],
        "additionalProperties": false
    }
    """;
}
