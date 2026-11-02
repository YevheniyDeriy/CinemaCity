using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CinemaCity.CatalogService.Tmdb;

internal sealed class NullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private const string TmdbDateFormat = "yyyy-MM-dd";

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a date string.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, TmdbDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(TmdbDateFormat, CultureInfo.InvariantCulture));
    }
}
