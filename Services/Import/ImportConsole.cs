namespace lionheart.Services.Import;

/// <summary>
/// The lines every import command prints about the catalogue: what it created, and which judgement
/// calls in its movement map it applied.
/// </summary>
internal static class ImportConsole
{
    public static void Growth(CatalogueGrowth growth)
    {
        Console.WriteLine($"\n{growth.Movements.Count} movements created.");
        foreach (var movement in growth.Movements.OrderBy(movement => movement.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {movement}");
        }

        Names("new movement bases", growth.Bases);
        Names("new equipment", growth.Equipment);
        Names("new modifiers", growth.Modifiers);
    }

    public static void Unsure(IReadOnlyList<MovementMapping> mappings, MovementMapFile file)
    {
        if (mappings.Count == 0) return;

        Console.WriteLine($"\n{mappings.Count} mappings applied that {file.FileName} marks unsure:");
        foreach (var mapping in mappings)
        {
            Console.WriteLine($"  {mapping.Name} -> {mapping.Movement}\n      {mapping.Unsure}");
        }
    }

    private static void Names(string what, IReadOnlyList<string> names)
    {
        if (names.Count > 0) Console.WriteLine($"{what}: {string.Join("; ", names.Order(StringComparer.OrdinalIgnoreCase))}");
    }
}
