using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Persistence;

public class RegexConverter : JsonConverter<Regex>
{
    public override Regex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var pattern = reader.GetString();
            if (pattern != null)
            {
                return new Regex(pattern);
            }
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                var root = doc.RootElement;
                var pattern = root.GetProperty("Pattern").GetString();
                var regexOptions = root.GetProperty("Options").GetString();
                if (pattern != null && regexOptions != null)
                {
                    return new Regex(pattern, (RegexOptions) Enum.Parse(typeof(RegexOptions), regexOptions));
                }
            }
        }

        throw new JsonException("Invalid JSON for Regex.");
    }

    public override void Write(Utf8JsonWriter writer, Regex value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Pattern", value.ToString());
        writer.WriteString("Options", value.Options.ToString());
        writer.WriteEndObject();
    }
}