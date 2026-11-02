namespace lionheart.Services.Oura.Import;

/// <summary>One archived record that could not be stored, and where to find it.</summary>
/// <remarks>
/// The file and line are the point: an import that says "47 records rejected" is not reportable,
/// and an import that silently drops them is worse. These identify the exact line to go and read.
/// </remarks>
public sealed record OuraImportRejection(string File, int Line, string Reason);

/// <summary>What became of one collection's archive file.</summary>
/// <remarks>
/// Two cases rather than a count that might mean "none in the file" or "no file at all". The
/// distinction is real and reportable: the resilience collection has no archived file, which is
/// why that collection's history starts whenever the live sync first runs, and a reader needs to
/// see that rather than infer it from a zero.
/// </remarks>
public abstract record OuraCollectionOutcome
{
    private protected OuraCollectionOutcome() { }

    public sealed record Read(
        int Lines,
        int Stored,
        IReadOnlyList<OuraImportRejection> Rejected) : OuraCollectionOutcome;

    public sealed record NoArchiveFile(string ExpectedName) : OuraCollectionOutcome;
}

public sealed record OuraCollectionImport(string Collection, OuraCollectionOutcome Outcome);

/// <summary>What an import run did, collection by collection.</summary>
public sealed record OuraImportReport(
    IReadOnlyList<OuraCollectionImport> Collections,
    IReadOnlyList<string> UncollectedFiles)
{
    public int Stored => Collections.Sum(StoredBy);

    public int Rejected => Collections.Sum(collection =>
        collection.Outcome is OuraCollectionOutcome.Read read ? read.Rejected.Count : 0);

    public IReadOnlyList<OuraImportRejection> Rejections =>
    [
        .. Collections
            .Select(collection => collection.Outcome)
            .OfType<OuraCollectionOutcome.Read>()
            .SelectMany(read => read.Rejected)
    ];

    private static int StoredBy(OuraCollectionImport collection) =>
        collection.Outcome is OuraCollectionOutcome.Read read ? read.Stored : 0;
}
