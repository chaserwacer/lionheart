namespace lionheart.Model;

/// <summary>
/// Constructor guards for domain records whose members carry closed-vocabulary or
/// always-present contracts.
/// </summary>
/// <remarks>
/// These exist because JSON TEXT columns are an untyped boundary: System.Text.Json will
/// happily build a record with an enum ordinal outside its members or a null in a
/// non-nullable member, and the result then travels through analytics as if it were real.
/// Guarding in the constructor covers every path (deserialization, <c>with</c>, app code)
/// rather than only the one the caller remembered to check.
/// </remarks>
public static class Require
{
    public static TEnum ClosedEnum<TEnum>(TEnum value, string name) where TEnum : struct, Enum =>
        Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"{typeof(TEnum).Name} has no member with this value.");

    /// <summary>Any reference-typed member the domain declares non-nullable: text, lists, vectors.</summary>
    public static T Present<T>(T? value, string name) where T : class =>
        value ?? throw new ArgumentNullException(name);

    /// <summary>
    /// A physical measure. NaN and the infinities are rejected at construction because nothing
    /// downstream can represent them: System.Text.Json refuses to write them, so an unguarded
    /// value surfaces much later as a serializer complaint raised during EF change tracking,
    /// blaming a layer that had nothing to do with it.
    /// </summary>
    public static double Finite(double value, string name) =>
        double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Measure must be a finite number.");
}
