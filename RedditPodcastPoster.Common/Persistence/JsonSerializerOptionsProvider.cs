using System.Text.Json;
using RedditPodcastPoster.Common.KnownTerms;

namespace RedditPodcastPoster.Common.Persistence;

public class JsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    public JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            Converters = {new RegexConverter()}
            // Update your JSON Serializer options here.
            //PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //Converters =
            //{
            //    new JsonStringEnumConverter()
            //},
            //IgnoreNullValues = true,
            //IgnoreReadOnlyFields = true
        };
    }
}