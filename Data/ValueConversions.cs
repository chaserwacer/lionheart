using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace lionheart.Data;

/// <summary>
/// Column-level conversions used by the training domain and the Oura tables alike: records as
/// JSON TEXT (polymorphic ones discriminated via [JsonDerivedType] attributes on the abstract
/// bases) and embedding vectors as BLOBs.
/// </summary>
public static class ValueConversions
{
    /// <summary>
    /// The exact options every JSON column is written and read with. Public so tests can pin
    /// the bytes that actually reach storage rather than whatever the ambient default produces.
    /// </summary>
    /// <remarks>
    /// The relaxed encoder is deliberate: these payloads go to a local SQLite file and are never
    /// interpolated into HTML, so the default encoder's HTML-safety escaping buys nothing and
    /// costs readability — plain training text like "sled pull+push" would otherwise be stored
    /// as "sled pull+push".
    /// </remarks>
    public static readonly JsonSerializerOptions StorageJson = new(JsonSerializerOptions.Default)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
    {
        var comparer = new ValueComparer<T>(
            (l, r) => Serialize(l) == Serialize(r),
            v => Serialize(v).GetHashCode(),
            v => Deserialize<T>(Serialize(v)));
        return property.HasConversion(v => Serialize(v), s => Deserialize<T>(s), comparer);
    }

    public static PropertyBuilder<float[]> HasVectorConversion(this PropertyBuilder<float[]> property)
    {
        var comparer = new ValueComparer<float[]>(
            (l, r) => l != null && r != null && l.SequenceEqual(r),
            v => v.Length, // cheap hash; vectors are write-once and compared by content rarely
            v => (float[])v.Clone());
        return property.HasConversion(v => ToBytes(v), b => ToFloats(b), comparer);
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, StorageJson);

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, StorageJson)
        ?? throw new JsonException($"Stored JSON deserialized to null for {typeof(T).Name}.");

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] ToFloats(byte[] bytes)
    {
        // A partial float means the BLOB is truncated. Decoding it anyway would hand analytics
        // a silently shortened vector, which compares as a legitimate (wrong) embedding.
        if (bytes.Length % sizeof(float) != 0)
            throw new InvalidDataException(
                $"Embedding BLOB length {bytes.Length} is not a whole number of {sizeof(float)}-byte floats.");

        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
