namespace lionheart.Model.Training.Sets;

/// <summary>What a lift was done for, beside what was lifted.</summary>
/// <remarks>
/// A kind rather than a separate set shape, because every kind is the same measurement — a
/// movement, a load and a count — and differs only in what the set means for records and for the
/// athlete reading it back. A warm-up never sets a record. A drop or a failure set was performed at
/// the weight stated, so it counts exactly as a working set does; the kind is kept because a
/// lifter reads "to failure" and "dropped from 225" as part of the set.
/// <para>
/// Member order is load-bearing: persisted as ints in set JSON, the way <see cref="WeightUnit"/> is.
/// <see cref="Working"/> is zero, so a lift stored before sets carried a kind reads as one.
/// </para>
/// </remarks>
public enum SetKind
{
    Working,
    WarmUp,
    Drop,
    Failure
}
