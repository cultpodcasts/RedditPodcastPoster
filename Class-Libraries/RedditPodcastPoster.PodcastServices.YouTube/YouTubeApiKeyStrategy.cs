using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeApiKeyStrategy(
    IOptions<YouTubeSettings> settings,
    IDateTimeService dateTimeService,
    ILogger<YouTubeApiKeyStrategy> logger)
    : IYouTubeApiKeyStrategy
{
    private readonly YouTubeSettings _settings = settings.Value;

    public Application GetApplication()
    {
        var settingsCount = _settings.Applications.Length;

        var applicationIndex = dateTimeService.GetHour() / (24 / settingsCount);

        var application = _settings.Applications.Skip(applicationIndex).First();
        logger.LogInformation(
            $"{nameof(GetApplication)}: Using application-key ending '{application.ApiKey.Substring(application.ApiKey.Length - 2)}'.");
        return application;
    }
}