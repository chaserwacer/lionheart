using System.Security.Cryptography;
using System.Text;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strong;

/// <summary>
/// Which workout a row of a Strong export belongs to.
/// </summary>
/// <remarks>
/// Strong exports no workout id, so its start and its name are the identity — the pair Strong
/// itself groups rows by. The start is the wall-clock time exactly as exported, with no offset,
/// because that text is what stays the same between two exports of the same history.
/// </remarks>
public sealed record StrongWorkoutKey(DateTime Start, string Name)
{
    /// <summary>
    /// The training session id this workout imports as, the same on every run.
    /// </summary>
    /// <remarks>
    /// A name-based (version 5) id rather than the time-ordered ones the session service mints, so
    /// a second import can tell a workout it already holds by key alone: the id is the "already
    /// imported" check, the way Oura's own document ids are for the Oura import. The athlete is
    /// part of the name, so one export imported for two athletes cannot collide.
    /// </remarks>
    public Guid SessionIdFor(Guid userId) =>
        NameBasedId($"{userId:D}|{Start:yyyy-MM-dd HH:mm:ss}|{Name}");

    /// <summary>
    /// The namespace every Strong workout id is derived under. Fixed forever: changing it would
    /// give every imported workout a new id and let the next run import the history twice.
    /// </summary>
    private static readonly Guid Namespace = new("6f1f3c0e-2a4b-5c8d-9e7f-0a1b2c3d4e5f");

    /// <summary>RFC 9562 version 5: SHA-1 over the namespace and the name, big-endian throughout.</summary>
    private static Guid NameBasedId(string name)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        Namespace.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

        var hash = SHA1.HashData([.. namespaceBytes, .. Encoding.UTF8.GetBytes(name)]);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}

/// <summary>What Strong's "Set Order" column says a set was.</summary>
/// <remarks>
/// Strong numbers its ordinary sets and marks the other three with a letter. Lionheart has no
/// field for any of the three, so what the import does with each is a decision, made in
/// <see cref="StrongSessions"/>.
/// </remarks>
public enum StrongSetKind
{
    Working,
    WarmUp,
    Drop,
    Failure
}

/// <summary>One set as Strong recorded it.</summary>
/// <remarks>
/// <see cref="Exercise"/> is Strong's name with its surrounding spaces removed — the name the
/// movement map is keyed by. <see cref="Note"/> is empty when the set carries none. The weight
/// carries no unit because the export carries none; see <see cref="StrongSessions.ExportUnit"/>.
/// </remarks>
public sealed record StrongSet(
    string Exercise,
    StrongSetKind Kind,
    double Weight,
    int Reps,
    Rpe Rpe,
    string Note);

/// <summary>One Strong workout, read whole from the rows that share its key.</summary>
/// <remarks>
/// <see cref="RestTimers"/> is a count rather than a list: Strong writes the rest between sets as
/// rows of their own, and the domain has nowhere to hold a rest, so the import only reports how
/// many it set aside.
/// </remarks>
public sealed record StrongWorkout(
    StrongWorkoutKey Key,
    Interval Period,
    string Notes,
    IReadOnlyList<StrongSet> Sets,
    int RestTimers);
