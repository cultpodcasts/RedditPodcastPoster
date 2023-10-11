using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Common.KnownTerms;

namespace RedditPodcastPoster.Common.Persistence;

public class JsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    public JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            Converters = {new RegexConverter()},
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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