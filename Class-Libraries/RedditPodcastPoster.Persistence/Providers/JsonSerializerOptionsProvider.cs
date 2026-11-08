using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Serialization;

namespace RedditPodcastPoster.Persistence.Providers;

public class JsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    public JsonSerializerOptions GetJsonSerializerOptions()
    {
        // Every DateTime / DateTimeOffset is read and written as UTC with a trailing Z (see UtcDateTimeConverter),
        // so Cosmos documents, Cosmos LINQ/query parameters and file backups all carry one date-time format.
        return new JsonSerializerOptions
        {
            Converters = { new RegexConverter(), new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }.AddUtcDateTimeConverters();
    }
}