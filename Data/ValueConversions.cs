using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace lionheart.Data;

/// <summary>
/// Column-level conversions for the training domain: polymorphic records as JSON TEXT
/// (discriminated via [JsonDerivedType] attributes on the abstract bases) and embedding
/// vectors as BLOBs.
/// </summary>
public static class ValueConversions
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Default;

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

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, Json);

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new JsonException($"Stored JSON deserialized to null for {typeof(T).Name}.");

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] ToFloats(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
