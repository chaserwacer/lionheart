using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace lionheart.Converters
{
    public class DateOnlyJsonConverter : JsonConverter<DateOnly>
    {
        private readonly string _format;
        public DateOnlyJsonConverter(string format = "yyyy-MM-dd") => _format = format;

        public override DateOnly Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException();
            var str = reader.GetString()!;
            if (DateOnly.TryParseExact(str, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            throw new JsonException($"Unable to parse DateOnly from “{str}”");
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateOnly value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString(_format, CultureInfo.InvariantCulture));
    }
}
