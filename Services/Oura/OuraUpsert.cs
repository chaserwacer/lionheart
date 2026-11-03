using lionheart.Model.Oura;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Oura;

/// <summary>
/// Staging a batch of Oura documents against what is already stored.
/// </summary>
/// <remarks>
/// Shared by the live sync and the archive importer, which face the same question: these documents
/// may be new, or may be Oura's newer version of ones already held. Because each table is keyed by
/// Oura's own document id, the answer needs no sync ledger — look the ids up, apply the fresh
/// payload to the ones found, insert the rest.
/// <para>
/// This stages; it never saves. The caller owns the transaction boundary, which is what lets the
/// importer put a whole collection in one.
/// </para>
/// </remarks>
internal static class OuraUpsert
{
    /// <summary>
    /// How many ids one existence query asks about.
    /// </summary>
    /// <remarks>
    /// A full archive import hands this method thousands of documents at once, and the same code
    /// serves a week-long sync handing it a dozen. Chunking keeps the generated <c>IN</c> list far
    /// below any provider's parameter ceiling without the small case paying for the large one.
    /// </remarks>
    private const int LookupChunk = 400;

    public static async Task UpsertAsync<TDocument, TReading>(
        this DbSet<TDocument> table, IReadOnlyList<TDocument> documents, CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        // One batch can carry the same document twice — Oura has issued duplicate pages, and a
        // caller may hand over overlapping ranges. Last wins, matching what a re-sync would do.
        var incoming = documents
            .GroupBy(document => document.OuraId)
            .ToDictionary(group => group.Key, group => group.Last());

        var stored = await StoredAsync<TDocument, TReading>(table, [.. incoming.Keys], ct);

        foreach (var (ouraId, document) in incoming)
        {
            if (stored.TryGetValue(ouraId, out var existing)) existing.Apply(document.Day, document.Reading);
            else table.Add(document);
        }
    }

    private static async Task<Dictionary<Guid, TDocument>> StoredAsync<TDocument, TReading>(
        DbSet<TDocument> table, IReadOnlyList<Guid> ouraIds, CancellationToken ct)
        where TDocument : OuraDocument<TReading>
        where TReading : class
    {
        var stored = new Dictionary<Guid, TDocument>();

        foreach (var chunk in ouraIds.Chunk(LookupChunk))
        {
            // Tracked on purpose: Apply mutates these, and the change tracker is what turns an
            // unchanged payload into no write at all.
            var found = await table.Where(document => chunk.Contains(document.OuraId)).ToListAsync(ct);

            foreach (var document in found) stored[document.OuraId] = document;
        }

        return stored;
    }
}
