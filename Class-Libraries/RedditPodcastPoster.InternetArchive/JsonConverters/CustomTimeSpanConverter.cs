using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.InternetArchive.JsonConverters;

public class CustomTimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        decimal val;
        if (reader.TokenType == JsonTokenType.String)
        {
            if (!decimal.TryParse(reader.GetString(), out val))
            {
                throw new JsonException($"Unable to parse decimal from '{reader.GetString()}'");
            }
        }
        else
        {
            val = reader.GetDecimal();
        }

        var seconds = Convert.ToInt32(Math.Truncate(val));
        var @base = TimeSpan.FromSeconds(seconds);
        return @base;
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        throw new NotImplementedException($"{nameof(Write)} is not implemented.");
    }
}