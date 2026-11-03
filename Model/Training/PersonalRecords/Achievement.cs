using System.Text.Json.Serialization;
using lionheart.Functional;
using lionheart.Model.Training.Sets;

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

    /// <summary>
    /// The most weight moved for <paramref name="Reps"/> reps, in a set of that many or more.
    /// </summary>
    /// <remarks>
    /// A rep max is held from <see cref="FewestReps"/> to <see cref="MostReps"/>. One rep is not a
    /// bracket of its own: every set contains a single, so the one-rep max is by definition the
    /// heaviest set, and a second kind for it would store one fact twice.
    /// </remarks>
    public sealed record RepMax(int Reps) : RecordKind
    {
        public const string Spelling = "repMax";

        /// <summary>The lowest rep count with a rep max of its own; below it is the heaviest set.</summary>
        public const int FewestReps = 2;

        /// <summary>
        /// The highest rep count a rep max is kept for.
        /// </summary>
        /// <remarks>
        /// The grid is a training tool: lifters work against their one through twelve, and past
        /// that the number has stopped describing strength and started describing work capacity,
        /// which the two volume records already answer. A longer set still holds every rep max up
        /// to here, because it contains a set of twelve.
        /// </remarks>
        public const int MostReps = 12;

        public override string Name => Spelling;

        public override int Bracket => Reps;

        /// <summary>
        /// Every rep max a set of <paramref name="setReps"/> reps holds.
        /// </summary>
        /// <remarks>
        /// A set holds the rep max at its own rep count and at every count below it, and this is
        /// not inference: a set of seven <em>contains</em> a set of six — the first six of the
        /// seven were actually performed. It is how lifters speak ("hit 105 for six, so that's a
        /// five-rep PR too"), and it is what lets a rep-max grid read as one: weight can only fall
        /// as the rep count rises, where a grid of exact counts can show a seven-rep max heavier
        /// than the six. The estimated one-rep max is the opposite case — a single nobody lifted —
        /// which is why it stays derived and labelled as an estimate rather than becoming a record.
        /// <para>
        /// The count stops at <see cref="MostReps"/>, so a set of twenty holds two through twelve,
        /// and starts at <see cref="FewestReps"/>, since the single it holds is the heaviest set's.
        /// </para>
        /// </remarks>
        public static IEnumerable<RepMax> HeldBy(int setReps) =>
            Enumerable.Range(FewestReps, MostReps - FewestReps + 1)
                .Where(reps => reps <= setReps)
                .Select(reps => new RepMax(reps));
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
        RepMax.Spelling => RepMaxAt(bracket, name),
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

    /// <remarks>
    /// Refused outside the held range rather than answered with an empty chain, because no record
    /// of that kind can exist: a one-rep max is asked for as the heaviest set, and nothing holds a
    /// rep max past the grid's last count.
    /// </remarks>
    private static Result<RecordKind, Problem> RepMaxAt(int? bracket, string name) => bracket switch
    {
        null or < 1 => Result<RecordKind, Problem>.Failure(
            new Problem.Invalid(nameof(bracket), $"'{name}' records need a rep count above zero.")),
        < RepMax.FewestReps => Result<RecordKind, Problem>.Failure(
            new Problem.Invalid(
                nameof(bracket), $"A one-rep max is the heaviest set; ask for '{HeaviestSet.Spelling}'.")),
        > RepMax.MostReps => Result<RecordKind, Problem>.Failure(
            new Problem.Invalid(nameof(bracket), $"'{name}' records are kept up to {RepMax.MostReps} reps.")),
        { } reps => Result<RecordKind, Problem>.Success(new RepMax(reps)),
    };
}

/// <summary>
/// What makes two records the same record: what it is held against, which question it answers,
/// and — for the weight kinds — the unit it is held in.
/// </summary>
/// <remarks>
/// The unit belongs here rather than being tested for comparability, and that is what stops a
/// movement's records locking to the first unit the athlete happened to log in. Weights never
/// convert, so a kilograms lift cannot beat a pounds record; without the unit in the identity it
/// also could not become one, and switching units would end that movement's progression for good.
/// A movement holds a kilograms record and a pounds record of the same kind, each set and beaten
/// within its own — which is how the iOS trends chart already keys its strength series, so a unit
/// switch starts a new line rather than putting a cliff in one.
/// <para>
/// Distances carry no unit here: they rank in meters, so one record covers every spelling of a
/// distance.
/// </para>
/// </remarks>
public sealed record RecordKey(RecordSubject Subject, RecordKind Kind, WeightUnit? HeldIn);

/// <summary>
/// What a record is the best of, carrying the performance that holds it.
/// </summary>
/// <remarks>
/// Fused rather than a kind beside a measurement, because each kind admits exactly one shape of
/// measurement: as two members, a heaviest-set record could hold a distance and the type system
/// would have nothing to say about it. Fused, the invalid pairings cannot be written down.
/// <para>
/// The three single-set lift kinds share one <see cref="LiftPerformance"/> rather than each
/// restating weight, unit and reps — they are three questions about the same set. It is always
/// the set as performed, so a rep max held by the front of a longer set still says which set.
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
    /// Which question this answers, bracket included. Abstract rather than switched over, so a
    /// case cannot be added that forgets to say what it is.
    /// </summary>
    [JsonIgnore]
    public abstract RecordKind Kind { get; }

    /// <summary>
    /// The weight unit this is measured in, where it is measured in one at all.
    /// </summary>
    /// <remarks>
    /// Part of a record's identity, not a detail of its measurement — see <see cref="RecordKey"/>.
    /// Absent for the distance kinds, which rank in meters and so have no unit to be held in.
    /// </remarks>
    [JsonIgnore]
    public abstract WeightUnit? HeldIn { get; }

    /// <summary>
    /// How good this is, as a number where higher is better — what one candidate is ranked against
    /// another by within one record's identity.
    /// </summary>
    /// <remarks>
    /// The time-based kinds negate, so "faster" and "heavier" both read as "larger". Meaningful
    /// only between two achievements of the same <see cref="RecordKey"/>, which is what
    /// <see cref="Beats"/> establishes first.
    /// </remarks>
    [JsonIgnore]
    public abstract double Rank { get; }

    /// <summary>
    /// Whether this is a better record than <paramref name="other"/>, which it can only be if the
    /// two are the same record.
    /// </summary>
    /// <remarks>
    /// Strictly better: equalling a record does not take it, which is also what stops a repeated
    /// set announcing itself every time.
    /// <para>
    /// Weights never convert, so <see cref="HeldIn"/> is part of the identity rather than a
    /// comparability test: 225 lb is the larger number and the lighter lift, and converting
    /// anywhere would announce a record that disagrees with the stored one. A kilograms lift is
    /// therefore not a worse pounds record — it is a different record, and sets and beats its own.
    /// </para>
    /// </remarks>
    public bool Beats(Achievement other) =>
        Kind == other.Kind && HeldIn == other.HeldIn && Rank > other.Rank;

    public sealed record HeaviestSet(LiftPerformance Lift) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.HeaviestSet();

        [JsonIgnore]
        public override double Rank => Lift.Weight;

        [JsonIgnore]
        public override WeightUnit? HeldIn => Lift.Unit;
    }

    public sealed record BestSetVolume(LiftPerformance Lift) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestSetVolume();

        [JsonIgnore]
        public override double Rank => Lift.Volume;

        [JsonIgnore]
        public override WeightUnit? HeldIn => Lift.Unit;
    }

    /// <summary>
    /// The heaviest weight held for <paramref name="Reps"/> reps, by <paramref name="Lift"/> — a set
    /// of exactly that many, or the front of a longer one.
    /// </summary>
    /// <remarks>
    /// The rep count and the set's own reps are kept apart because a lifter reads them apart: a
    /// five-rep max from a clean set of five and one from the first five of a set of eight are the
    /// same record and different stories. <see cref="RecordKind.RepMax.HeldBy"/> is what keeps
    /// <paramref name="Reps"/> at or below the set's.
    /// </remarks>
    public sealed record RepMax(int Reps, LiftPerformance Lift) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.RepMax(Reps);

        [JsonIgnore]
        public override double Rank => Lift.Weight;

        [JsonIgnore]
        public override WeightUnit? HeldIn => Lift.Unit;
    }

    public sealed record BestSessionVolume(SessionVolume Total) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestSessionVolume();

        [JsonIgnore]
        public override double Rank => Total.Volume;

        [JsonIgnore]
        public override WeightUnit? HeldIn => Total.Unit;
    }

    public sealed record LongestDistance(DistanceEffort Effort) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.LongestDistance();

        [JsonIgnore]
        public override double Rank => Effort.DistanceInMeters;

        [JsonIgnore]
        public override WeightUnit? HeldIn => null;
    }

    public sealed record FastestTime(DistanceEffort Effort) : Achievement
    {
        /// <summary>The bracket is the distance actually covered, so the time is one the athlete ran.</summary>
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.FastestTime(Effort.BracketMeters);

        [JsonIgnore]
        public override double Rank => -Effort.Duration.TotalSeconds;

        [JsonIgnore]
        public override WeightUnit? HeldIn => null;
    }

    public sealed record BestPace(DistanceEffort Effort) : Achievement
    {
        [JsonIgnore]
        public override RecordKind Kind => new RecordKind.BestPace();

        [JsonIgnore]
        public override double Rank => -Effort.SecondsPerMeter;

        [JsonIgnore]
        public override WeightUnit? HeldIn => null;
    }
}
