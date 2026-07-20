namespace lionheart.Services.Interpretation;

/// <summary>
/// Identifies the extractor that produced a decoration. Every decoration and analyzable entry
/// is stamped with it so re-decoration can find stale rows (<c>WHERE DecoratorVersion &lt; Current</c>).
/// Bump it whenever a change to the parse prompt or facet axes would produce different
/// annotations for the same text.
/// </summary>
public static class DecoratorVersion
{
    public const int Current = 1;
}
