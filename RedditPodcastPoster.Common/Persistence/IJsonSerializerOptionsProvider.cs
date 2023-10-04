using System.Text.Json;

namespace RedditPodcastPoster.Common.Persistence;

public interface IJsonSerializerOptionsProvider
{
    JsonSerializerOptions GetJsonSerializerOptions();
}