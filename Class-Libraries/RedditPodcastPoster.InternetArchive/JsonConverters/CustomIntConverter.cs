using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.InternetArchive.JsonConverters;

public class CustomIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (int.TryParse(reader.GetString(), out var i))
            {
                return i;
            }
        }

        return reader.GetInt32();
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        throw new NotImplementedException($"{nameof(Write)} is not implemented.");
    }
}