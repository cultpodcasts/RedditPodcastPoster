using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeApiKeyStrategy(
    IOptions<YouTubeSettings> settings,
    ILogger<YouTubeApiKeyStrategy> logger)
    : IYouTubeApiKeyStrategy
{
    private readonly YouTubeSettings _settings = settings.Value;

    public Application GetApplication()
    {
        return _settings.Applications.First();
    }
}