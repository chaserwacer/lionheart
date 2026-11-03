using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using lionheart.Services.Llm;

namespace lionheart.Services.Interpretation;

public interface IInterpretationParser
{
    Task<Result<ParsedInterpretation, Problem>> ParseAsync(InterpretationRequest request, string knownMovementContext);
}

/// <summary>
/// Stage one of the engine: raw text becomes drafts plus a proposed decoration. The parser
/// never invents structure it cannot support — an utterance it can't break down stays a
/// <see cref="DraftFreeformSet"/> rather than becoming a badly guessed lift.
/// </summary>
public sealed class InterpretationParser : IInterpretationParser
{
    /// <summary>
    /// Generous: the parse is one JSON object, but a log describing several entries with their sets
    /// is long, and a truncated object is unreadable rather than merely shorter.
    /// </summary>
    private const int ParseTokenBudget = 8192;

    private readonly IChatModel _chatModel;

    public InterpretationParser(IChatModel chatModel) => _chatModel = chatModel;

    public Task<Result<ParsedInterpretation, Problem>> ParseAsync(
        InterpretationRequest request, string knownMovementContext) =>
        string.IsNullOrWhiteSpace(request.RawText)
            ? Task.FromResult(Result<ParsedInterpretation, Problem>.Failure(
                new Problem.Invalid(nameof(request.RawText), "Input text cannot be empty.")))
            : RequestParseAsync(request, knownMovementContext)
                .BindAsync(parse => MapToDrafts(parse, request.LoggedAt));

    /// <summary>
    /// The model call. Asks for a schema-conformant object through the provider's structured-output
    /// path — notably <b>not</b> by forcing a tool call, which Anthropic answers with a 400 — and for
    /// the shallowest reasoning available, which is what keeps the answer in the content.
    /// </summary>
    private Task<Result<LlmParse, Problem>> RequestParseAsync(
        InterpretationRequest request, string knownMovementContext) =>
        _chatModel
            .RespondAsync(ChatTurnRequest.Extraction(
                systemPrompt: BuildSystemPrompt(request.LoggedAt, knownMovementContext),
                userText: request.RawText,
                schemaName: "interpretation",
                schemaJson: ParseSchema,
                maxOutputTokens: ParseTokenBudget))
            .BindAsync(ReadParse);

    private static Result<LlmParse, Problem> ReadParse(ModelTurn turn)
    {
        if (turn is not ModelTurn.Answered answered)
        {
            return Result<LlmParse, Problem>.Failure(new Problem.Upstream(
                "The parser model asked to call a tool; extraction offers none."));
        }

        var json = answered.Text;
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<LlmParse, Problem>.Failure(new Problem.Upstream(
                "Parser returned an empty response. If the configured model reasons, the content " +
                "may have been routed to its reasoning channel instead — see the \"Traps\" section " +
                "of docs/data-remodel.md."));
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LlmParse>(json, JsonOptions);
            return parsed is null
                ? Result<LlmParse, Problem>.Failure(
                    new Problem.Upstream("Parser returned no interpretable content."))
                : Result<LlmParse, Problem>.Success(parsed);
        }
        catch (JsonException ex)
        {
            return Result<LlmParse, Problem>.Failure(
                new Problem.Upstream($"Parser returned malformed JSON: {ex.Message}"));
        }
    }

    private static Result<ParsedInterpretation, Problem> MapToDrafts(LlmParse parse, DateTime loggedAt) =>
        (parse.Entries ?? [])
            .Traverse(entry => MapEntry(entry, loggedAt))
            .Map(entries => new ParsedInterpretation(entries, parse.Warnings ?? []));

    private static Result<DraftEntry, Problem> MapEntry(LlmEntry entry, DateTime loggedAt) =>
        MapPeriod(entry, loggedAt).Map(period => Draft(entry, period));

    private static DraftEntry Draft(LlmEntry entry, Period period)
    {
        var decoration = MapDecoration(entry.Decoration);
        var notes = entry.Notes ?? string.Empty;

        // Perceived-effort ratings are a numeric self-report from the UI, never something
        // to guess from prose — text sentiment belongs in the decoration's valence instead.
        return entry.Kind switch
        {
            "session" => new DraftSession([.. (entry.Sets ?? []).Select(MapSet)], new EffortNotReported())
            {
                Period = period,
                Notes = notes,
                Decoration = decoration
            },
            "exertionContext" => new DraftExertionContext(new EffortNotReported())
            {
                Period = period,
                Notes = notes,
                Decoration = decoration
            },
            _ => new DraftStateContext
            {
                Period = period,
                Notes = notes,
                Decoration = decoration
            }
        };
    }

    private static Result<Period, Problem> MapPeriod(LlmEntry entry, DateTime loggedAt)
    {
        var start = entry.Start ?? loggedAt;
        if (entry.End is not { } end) return Result<Period, Problem>.Success(new Instant(start));

        return end < start
            ? Result<Period, Problem>.Failure(new Problem.Upstream(
                $"Parsed period ends ({end:o}) before it starts ({start:o})."))
            : Result<Period, Problem>.Success(new Interval(start, end));
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
