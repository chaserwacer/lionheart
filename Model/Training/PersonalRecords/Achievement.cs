using System.Text.Json.Serialization;
using lionheart.Functional;

namespace lionheart.Model.Training.PersonalRecords;

/// <summary>
/// Which question a record answers, and what distinguishes two records answering the same one.
/// </summary>
/// <remarks>
/// Together with a <see cref="RecordSubject"/> this is a record's identity: exactly one record of
/// a kind stands on a subject at a time. Two kinds are bracketed, because "my five-rep max" and
/// "my three-rep max" are separate records on one movement — which is the whole reason a rep-max
/// grid exists and two numbers were never enough.
/// <para>
/// Value equality is load-bearing: these are used directly as lookup keys when a session's
/// candidates are matched against what already stands.
/// </para>
/// </remarks>
public abstract record RecordKind
{
    private protected RecordKind() { }

    /// <summary>
    /// The stable spelling of this kind — the queryable column, the history route's parameter and
    /// the wire discriminator all use it.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// What tells two records of this kind on one subject apart, or zero where nothing does.
    /// </summary>
    /// <remarks>
    /// Kept alongside <see cref="Name"/> in a column so one chain can be read back without
    /// deserializing every record the athlete holds to find out which kind each of them is.
    /// </remarks>
    public abstract int Bracket { get; }

    /// <summary>The most weight moved in a single set, at any rep count.</summary>
    public sealed record HeaviestSet : RecordKind
    {
        public const string Spelling = "heaviestSet";

        public override string Name => Spelling;

        public override int Bracket => 0;
    }

    /// <summary>The most weight times reps in a single set.</summary>
    public sealed record BestSetVolume : RecordKind
    {
        public const string Spelling = "bestSetVolume";

        public override string Name => Spelling;

        public override int Bracket => 0;
    }

    /// <summary>The most weight moved for exactly <paramref name="Reps"/> reps.</summary>
    public sealed record RepMax(int Reps) : RecordKind
    {
        public const string Spelling = "repMax";

        public override string Name => Spelling;

        public override int Bracket => Reps;
    }

    /// <summary>The most weight times reps summed over one session.</summary>
    public sealed record BestSessionVolume : RecordKind
    {
        public const string Spelling = "bestSessionVolume";

        public override string Name => Spelling;

        public override int Bracket => 0;
    }

    /// <summary>The furthest covered in a single effort.</summary>
    public sealed record LongestDistance : RecordKind
    {
        public const string Spelling = "longestDistance";

        public override string Name => Spelling;

        public override int Bracket => 0;
    }

    /// <summary>The quickest cover of <paramref name="DistanceMeters"/>.</summary>
    public sealed record FastestTime(int DistanceMeters) : RecordKind
    {
        public const string Spelling = "fastestTime";

        public override string Name => Spelling;

        public override int Bracket => DistanceMeters;
    }

    /// <summary>The quickest time per unit distance, over any distance.</summary>
    public sealed record BestPace : RecordKind
    {
        public const string Spelling = "bestPace";

        public override string Name => Spelling;

        public override int Bracket => 0;
    }

    /// <summary>
    /// The kind a client named, or a refusal saying which part of the ask was wrong.
    /// </summary>
    /// <remarks>
    /// The partial entry point: this is the one place a kind arrives from outside and the only
    /// one that has to answer for a spelling nobody defined. A bracketed kind needs its bracket
    /// and an unbracketed one has no use for it, so each is refused separately rather than
    /// quietly defaulted — asking for "my rep max" without saying at how many reps is a question
    /// with no answer, not a question about one rep.
    /// </remarks>
    public static Result<RecordKind, Problem> Of(string name, int? bracket) => name switch
    {
        HeaviestSet.Spelling => Unbracketed(new HeaviestSet(), bracket, name),
        BestSetVolume.Spelling => Unbracketed(new BestSetVolume(), bracket, name),
        BestSessionVolume.Spelling => Unbracketed(new BestSessionVolume(), bracket, name),
        LongestDistance.Spelling => Unbracketed(new LongestDistance(), bracket, name),
        BestPace.Spelling => Unbracketed(new BestPace(), bracket, name),
        RepMax.Spelling => Bracketed(bracket, name, "a rep count", reps => new RepMax(reps)),
        FastestTime.Spelling => Bracketed(bracket, name, "a distance in meters", m => new FastestTime(m)),
        _ => Result<RecordKind, Problem>.Failure(
            new Problem.Invalid(nameof(name), $"'{name}' is not a kind of personal record."))
    };

    private static Result<RecordKind, Problem> Unbracketed(RecordKind kind, int? bracket, string name) =>
        bracket is null
            ? Result<RecordKind, Problem>.Success(kind)
            : Result<RecordKind, Problem>.Failure(
                new Problem.Invalid(nameof(bracket), $"'{name}' records are not bracketed."));

    private static Result<RecordKind, Problem> Bracketed(
        int? bracket, string name, string expected, Func<int, RecordKind> kind) =>
        bracket is { } value and > 0
            ? Result<RecordKind, Problem>.Success(kind(value))
            : Result<RecordKind, Problem>.Failure(
                new Problem.Invalid(nameof(bracket), $"'{name}' records need {expected} above zero."));

    /// <summary>
    /// The highest rep count the grid keeps a record for.
    /// </summary>
    /// <remarks>
    /// A grid is a training tool: lifters work against their one through twelve, and past that the
    /// number has stopped describing strength and started describing work capacity, which the two
    /// volume records already answer. Unbounded, a single 23-rep set would plant a row nothing ever
    /// beats and that sits in the summary forever. A set above the ceiling is neither refused nor
    /// lost — it sets no rep max, while still competing for the heaviest set, the best set volume
    /// and the session total.
    /// </remarks>
    public const int HighestGradedRepCount = 12;
}

/// <summary>
/// What a record is the best of, carrying the performance that holds it.
/// </summary>
/// <remarks>
/// Fused rather than a kind beside a measurement, because each kind admits exactly one shape of
/// measurement: as two members, a heaviest-set record could hold a distance and the type system
/// would have nothing to say about it. Fused, the invalid pairings cannot be written down.
/// <para>
/// The three single-set lift kinds share one <see cref="LiftPerformance"/> rather than each
/// restating weight, unit and reps — they are three questions about the same set.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(HeaviestSet), "heaviestSet")]
[JsonDerivedType(typeof(BestSetVolume), "bestSetVolume")]
[JsonDerivedType(typeof(RepMax), "repMax")]
[JsonDerivedType(typeof(BestSessionVolume), "bestSessionVolume")]
[JsonDerivedType(typeof(LongestDistance), "longestDistance")]
[JsonDerivedType(typeof(FastestTime), "fastestTime")]
[JsonDerivedType(typeof(BestPace), "bestPace")]
public abstract record Achievement
{
    private protected Achievement() { }

    /// <summary>
    /// Which record this is, bracket included. Abstract rather than switched over, so a case
    /// cannot be added that forgets to say what it is.
    /// </summary>
    [JsonIgnore]
    public abstract RecordKind Kind { get; }

    /// <summary>
    /// How good this is, as a number where higher is better — what one candidate is ranked against
    /// another by within a single kind.
    /// </summary>
    /// <remarks>
    /// The time-based kinds negate, so "faster" and "heavier" both read as "larger". Meaningful
    /// only within a kind, and for the weight kinds only within one unit, so <see cref="Beats"/>
    /// is the member domain logic compares with.
    /// </remarks>
    [JsonIgnore]
    public abstract double Rank { get; }

    /// <summary>
    /// Whether this is a better record than <paramref name="other"/> of the same kind.
    /// </summary>
    /// <remarks>
    /// Strictly better: equalling a record does not take it, which is also what stops a repeated
    /// set announcing itself every time.
    /// <para>
    /// A weight in a unit the standing record is not held in does not beat it. The server stores
    /// each record's unit and compares the numbers without converting, so converting anywhere
    /// would announce a record that disagrees with the stored one — 225 lb is the larger number
    /// and the lighter lift. Both clients compare the same way, which is why this is the rule and
    /// not a limitation.
    /// </para>
    /// </remarks>
    public bool Beats(Achievement other) =>
        Kind == other.Kind && ComparableWith(other) && Rank > other.Rank;

    /// <summary>
    /// Whether the two are measured in units that may be compared at all. Distances always are;
    /// weights only within one unit.
    /// </summary>
    protected abstract bool ComparableWith(Achievement other);

    public sealed record HeaviestSet(LiftPerformance Lift) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.HeaviestSet();

        [JsonIgnore]
        public override double Rank => Lift.Weight;

        protected override bool ComparableWith(Achievement other) =>
            other is HeaviestSet o && o.Lift.Unit == Lift.Unit;
    }

    public sealed record BestSetVolume(LiftPerformance Lift) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestSetVolume();

        [JsonIgnore]
        public override double Rank => Lift.Volume;

        protected override bool ComparableWith(Achievement other) =>
            other is BestSetVolume o && o.Lift.Unit == Lift.Unit;
    }

    public sealed record RepMax(LiftPerformance Lift) : Achievement
    {
        /// <summary>The rep count is the set's own, so a grid row cannot disagree with the set in it.</summary>
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.RepMax(Lift.Reps);

        [JsonIgnore]
        public override double Rank => Lift.Weight;

        protected override bool ComparableWith(Achievement other) =>
            other is RepMax o && o.Lift.Unit == Lift.Unit;
    }

    public sealed record BestSessionVolume(SessionVolume Total) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestSessionVolume();

        [JsonIgnore]
        public override double Rank => Total.Volume;

        protected override bool ComparableWith(Achievement other) =>
            other is BestSessionVolume o && o.Total.Unit == Total.Unit;
    }

    public sealed record LongestDistance(DistanceEffort Effort) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.LongestDistance();

        [JsonIgnore]
        public override double Rank => Effort.DistanceInMeters;

        protected override bool ComparableWith(Achievement other) => other is LongestDistance;
    }

    public sealed record FastestTime(DistanceEffort Effort) : Achievement
    {
        /// <summary>The bracket is the distance actually covered, so the time is one the athlete ran.</summary>
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.FastestTime(Effort.BracketMeters);

        [JsonIgnore]
        public override double Rank => -Effort.Duration.TotalSeconds;

        protected override bool ComparableWith(Achievement other) => other is FastestTime;
    }

    public sealed record BestPace(DistanceEffort Effort) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestPace();

        [JsonIgnore]
        public override double Rank => -Effort.SecondsPerMeter;

        protected override bool ComparableWith(Achievement other) => other is BestPace;
    }
}
