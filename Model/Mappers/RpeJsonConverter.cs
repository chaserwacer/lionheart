using System.Text.Json;
using System.Text.Json.Serialization;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Mappers;

/// <summary>
/// Maps <see cref="Rpe"/> fill-state to JSON: rated → number, unrated → null.
/// </summary>
public class RpeJsonConverter : JsonConverter<Rpe>
{
    public override bool HandleNull => true;

    public override Rpe Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Null ? Rpe.Unrated : Rpe.Of(reader.GetDouble());
    }

    public override void Write(Utf8JsonWriter writer, Rpe value, JsonSerializerOptions options)
    {
        if (value.IsRated) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}
