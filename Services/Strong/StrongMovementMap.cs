using lionheart.Functional;
using lionheart.Services.Import;

namespace lionheart.Services.Strong;

/// <summary>
/// The map from Strong's exercise names to Lionheart movements, <c>strong-movements.json</c>
/// beside this file.
/// </summary>
/// <remarks>
/// Keyed by "strong": the name exactly as Strong exports it.
/// </remarks>
public static class StrongMovementMap
{
    public static MovementMapFile File { get; } = new(
        "strong-movements.json", "lionheart.Services.Strong.strong-movements.json", "strong");

    public static Result<MovementMap, Problem> Shipped() => MovementMap.Shipped(File);

    public static Result<MovementMap, Problem> Parse(string json) => MovementMap.Parse(json, File);
}
