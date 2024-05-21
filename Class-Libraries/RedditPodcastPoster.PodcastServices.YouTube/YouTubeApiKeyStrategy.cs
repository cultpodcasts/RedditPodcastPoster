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
        var application = _settings.Applications.Skip(DateTime.UtcNow.Hour <= 11 ? 0 : 1).First();
        logger.LogInformation(
            $"{nameof(GetApplication)}: Using application-key ending '{application.ApiKey.Substring(application.ApiKey.Length - 2)}'.");
        return application;
    }
}