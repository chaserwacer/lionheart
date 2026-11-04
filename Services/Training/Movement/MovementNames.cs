namespace lionheart.Services.Training.Balance;

/// <summary>A movement's name as one phrase, the way the iOS client's display name reads it.</summary>
public static class MovementNames
{
    /// <summary>"Incline Dumbell Bench Press": the modifier, then the equipment unless the base already says it, then the base.</summary>
    public static string Of(string baseName, string equipment, string? modifier)
    {
        var words = new List<string>();
        if (modifier is { Length: > 0 }) words.Add(char.ToUpperInvariant(modifier[0]) + modifier[1..]);
        if (equipment.Length > 0 && !baseName.Contains(equipment, StringComparison.OrdinalIgnoreCase)) words.Add(equipment);
        words.Add(baseName);
        return string.Join(' ', words);
    }
}
