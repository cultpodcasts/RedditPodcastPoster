using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.InternetArchive.JsonConverters;

public class RelativeUriConverter : JsonConverter<Uri>
{
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var uriString = reader.GetString()
            ?? throw new JsonException("Expected a relative URI string.");
        return new Uri(uriString, UriKind.Relative);
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        throw new NotImplementedException($"{nameof(Write)} is not implemented.");
    }
}