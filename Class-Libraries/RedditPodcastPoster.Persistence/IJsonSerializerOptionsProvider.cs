using System.Text.Json;

namespace RedditPodcastPoster.Persistence;

public interface IJsonSerializerOptionsProvider
{
    JsonSerializerOptions GetJsonSerializerOptions();
}