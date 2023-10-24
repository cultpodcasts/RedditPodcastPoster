using System.Text.Json;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IJsonSerializerOptionsProvider
{
    JsonSerializerOptions GetJsonSerializerOptions();
}