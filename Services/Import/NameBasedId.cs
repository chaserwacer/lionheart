using System.Security.Cryptography;
using System.Text;

namespace lionheart.Services.Import;

/// <summary>
/// Ids an import derives from what it imports, so the same thing gets the same id on every run.
/// </summary>
/// <remarks>
/// That is how an importer recognises what it already holds: the id is the "already imported"
/// check. Each importer keeps its own namespace, fixed forever — changing one gives everything
/// imported under it a new id and lets the next run import the history twice.
/// </remarks>
internal static class NameBasedId
{
    /// <summary>RFC 9562 version 5: SHA-1 over the namespace and the name, big-endian throughout.</summary>
    public static Guid Of(Guid @namespace, string name)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        @namespace.TryWriteBytes(namespaceBytes, bigEndian: true, out _);

        var hash = SHA1.HashData([.. namespaceBytes, .. Encoding.UTF8.GetBytes(name)]);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
