using System.Text.Json;

namespace RedditPodcastPoster.Persistence.Abstractions.Providers;

public interface IJsonSerializerOptionsProvider
{
    JsonSerializerOptions GetJsonSerializerOptions();
}