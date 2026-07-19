using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Sets;

/// <summary>
/// How an entry was logged. Structuring is additive enrichment: a <see cref="Texted"/>
/// source survives any upgrade to a richer set type — the words are never lost.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Texted), "texted")]
[JsonDerivedType(typeof(Formed), "formed")]
public abstract record Utterance;

public sealed record Texted : Utterance
{
    public Texted(string rawText, double structureConfidence)
    {
        if (structureConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(structureConfidence), "Confidence must be within [0, 1].");
        RawText = rawText;
        StructureConfidence = structureConfidence;
    }

    public string RawText { get; }

    /// <summary>
    /// Parser's confidence in any structure lifted from the text ("2 or 3 sets" logs
    /// low-confidence rather than blocking). Meaningful only when structure was extracted.
    /// </summary>
    public double StructureConfidence { get; }
}

public sealed record Formed : Utterance;
