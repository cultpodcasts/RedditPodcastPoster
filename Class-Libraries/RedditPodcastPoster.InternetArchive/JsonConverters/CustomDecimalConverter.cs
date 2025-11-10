using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.InternetArchive.JsonConverters;

public class CustomDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (decimal.TryParse(reader.GetString(), out var dec))
            {
                return dec;
            }
        }
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        throw new NotImplementedException($"{nameof(Write)} is not implemented.");
    }
}