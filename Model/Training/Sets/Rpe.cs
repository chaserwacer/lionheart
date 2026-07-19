using System.Text.Json.Serialization;
using lionheart.Model.Mappers;

namespace lionheart.Model.Training.Sets;

/// <summary>
/// Rate of perceived exertion with explicit fill-state: <see cref="Unrated"/> or
/// <see cref="Of"/> a value in [0, 10]. Never null, never a magic zero.
/// </summary>
[JsonConverter(typeof(RpeJsonConverter))]
public readonly record struct Rpe
{
    private readonly double _value;

    private Rpe(bool isRated, double value)
    {
        IsRated = isRated;
        _value = value;
    }

    public bool IsRated { get; }

    public double Value => IsRated
        ? _value
        : throw new InvalidOperationException("Rpe is unrated; check IsRated before reading Value.");

    public static Rpe Unrated => default;

    public static Rpe Of(double value) => value is >= 0 and <= 10
        ? new Rpe(true, value)
        : throw new ArgumentOutOfRangeException(nameof(value), "RPE must be within [0, 10].");
}
