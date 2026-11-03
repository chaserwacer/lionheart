using lionheart.Functional;
using lionheart.Services.Import;

namespace lionheart.Services.CoachedSheets;

/// <summary>
/// The map from training-os's exercise slugs to Lionheart movements, <c>coached-movements.json</c>
/// beside this file.
/// </summary>
/// <remarks>
/// Keyed by "slug": training-os's canonical name for the exercise, which already folds the sheets'
/// spellings of one lift together.
/// </remarks>
public static class SheetMovementMap
{
    public static MovementMapFile File { get; } = new(
        "coached-movements.json", "lionheart.Services.CoachedSheets.coached-movements.json", "slug");

    public static Result<MovementMap, Problem> Shipped() => MovementMap.Shipped(File);
}
