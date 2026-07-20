using System.Text.Json;

namespace lionheart.Tests.Support;

public static class JsonAssert
{
    /// <summary>
    /// Structural equality through the storage codec. Record equality is reference-based on
    /// the collection members these types carry, so rehydrated instances are compared by
    /// their serialized form — the exact representation persistence must preserve.
    /// </summary>
    public static void Equal<T>(T expected, T actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
}
