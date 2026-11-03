using System.Diagnostics;
using System.Text.Json.Serialization;

namespace lionheart.Model.Analysis;

/// <summary>
/// A decoration as a client may read it. The whole job of this type is hiding the two members of
/// <see cref="Decoration"/> that belong to the analysis engine rather than to the athlete:
/// <see cref="Decoration.SummaryText"/>, which exists to be embedded, and
/// <see cref="Decoration.DecoratorVersion"/>, which only the re-decoration pass acts on. The
/// vector itself never leaves the <see cref="AnalyzableEntry"/> projection at all.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NotDecoratedView), "none")]
[JsonDerivedType(typeof(DecoratedView), "decorated")]
public abstract record DecorationView
{
    private protected DecorationView() { }

    public static DecorationView Of(DecorationState state) => state switch
    {
        NotDecorated => new NotDecoratedView(),
        Decorated decorated => new DecoratedView(
            decorated.Decoration.Facets,
            decorated.Decoration.FreeTags,
            decorated.Decoration.Valence),
        _ => throw new UnreachableException()
    };
}

public sealed record NotDecoratedView : DecorationView;

public sealed record DecoratedView(
    IReadOnlyList<Facet> Facets,
    IReadOnlyList<string> FreeTags,
    Valence Valence) : DecorationView;
