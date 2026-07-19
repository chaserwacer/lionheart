namespace lionheart.Model.Analysis;

/// <summary>
/// Domain contract for anything the analytics/RAG layer consumes: a decoration and a
/// place on the timeline. Implementors also project into <see cref="AnalyzableEntry"/>
/// when decorated — the interface is the domain-side contract, the entry is the queryable
/// read model.
/// </summary>
public interface IAnalyzable
{
    DecorationState Decoration { get; }
    Period Period { get; }
}
