using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Serialization;

namespace RedditPodcastPoster.Persistence.Providers;

public class JsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    public JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            Converters = { new RegexConverter(), new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}