using System.Text.RegularExpressions;
using lionheart.Services.Import;

namespace lionheart.Services.Training.Movement;

/// <summary>
/// A movement's name as one phrase, by the library's convention: the variation, the exercise, then
/// the equipment in parentheses — "Seated Leg Curl (Machine)", the way Strong and a gym say it.
/// </summary>
/// <remarks>
/// The iOS client composes the same phrase (<c>MovementData.displayName</c>); the two are kept in
/// step by their tests. A part another part already says is not said again, so names that overlap
/// cannot read twice: the modifier is dropped when the base already says it ("Incline Bench Press"
/// with "incline"), and the equipment when the title already says it ("T-Bar Row", on a T-Bar).
/// <para>
/// A modifier is stored lower case, whoever created it, so its words are capitalised here, short
/// joining words apart ("Behind-the-Back"). The base and the equipment keep the capitalisation
/// they are stored with, which is the athlete's.
/// </para>
/// </remarks>
public static partial class MovementNames
{
    private static readonly HashSet<string> JoiningWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "and", "of", "on", "the", "to",
    };

    /// <summary>"Seated Leg Curl (Machine)": the title, then the equipment unless the title already says it.</summary>
    public static string Of(string baseName, string equipment, string? modifier)
    {
        var title = TitleOf(baseName, modifier);
        var kit = equipment.Trim();
        return kit.Length == 0 || Says(title, kit) ? title : $"{title} ({kit})";
    }

    public static string Of(MovementName name) => Of(name.Base, name.Equipment, name.Modifier);

    /// <summary>"Seated Leg Curl": the modifier qualifying the base, unless the base already says it.</summary>
    public static string TitleOf(string baseName, string? modifier)
    {
        var exercise = baseName.Trim();
        var variation = modifier?.Trim() ?? string.Empty;
        return variation.Length == 0 || Says(exercise, variation) ? exercise : $"{Capitalised(variation)} {exercise}";
    }

    /// <summary>Every word capitalised but a joining word inside the phrase: "behind-the-back" reads "Behind-the-Back".</summary>
    public static string Capitalised(string modifier) =>
        Word().Replace(modifier, match => match.Index > 0 && JoiningWords.Contains(match.Value)
            ? match.Value
            : char.ToUpperInvariant(match.Value[0]) + match.Value[1..]);

    /// <summary>Whether <paramref name="phrase"/> holds <paramref name="part"/> as whole words, ignoring case.</summary>
    private static bool Says(string phrase, string part)
    {
        var words = Words(phrase);
        var wanted = Words(part);
        return Enumerable.Range(0, Math.Max(0, words.Length - wanted.Length + 1))
            .Any(start => words.Skip(start).Take(wanted.Length).SequenceEqual(wanted, StringComparer.OrdinalIgnoreCase));
    }

    private static string[] Words(string phrase) => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"\b\p{L}[\p{L}']*")]
    private static partial Regex Word();
}
