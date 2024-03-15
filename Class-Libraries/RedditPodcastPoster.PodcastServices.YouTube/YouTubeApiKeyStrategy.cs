using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeApiKeyStrategy(
    IOptions<YouTubeSettings> settings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeApiKeyStrategy> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IYouTubeApiKeyStrategy
{
    private readonly YouTubeSettings _settings = settings.Value;

    public Application GetApplication()
    {
        return _settings.Applications.Skip(DateTime.UtcNow.Hour <= 11 ? 0 : 1).First();
    }
}