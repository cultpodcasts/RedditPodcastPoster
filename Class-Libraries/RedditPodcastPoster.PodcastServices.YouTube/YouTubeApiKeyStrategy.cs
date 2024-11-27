using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeApiKeyStrategy(
    IOptions<YouTubeSettings> settings,
    IDateTimeService dateTimeService,
    ILogger<YouTubeApiKeyStrategy> logger)
    : IYouTubeApiKeyStrategy
{
    private readonly YouTubeSettings _settings =
        settings.Value ?? throw new ArgumentNullException($"Missing {nameof(YouTubeSettings)}.");

    public Application GetApplication(ApplicationUsage usage)
    {
        logger.LogInformation($"Get youtube-applications for usage '{usage}'.");
        var usageApplications = _settings.Applications.Where(x => x.Usage.HasFlag(usage)).ToArray();
        var settingsCount = usageApplications.Count();
        if (settingsCount == 0)
        {
            throw new InvalidOperationException($"No youtube-applications registered or usage '{usage.ToString()}'");
        }

        var applicationIndex = dateTimeService.GetHour() / (24 / settingsCount);
        logger.LogInformation($"Using key '{applicationIndex}' out of '{settingsCount}' application-keys.");

        var application = usageApplications.Skip(applicationIndex).First();
        logger.LogInformation(
            $"{nameof(GetApplication)}: Using application-key with name '{application.DisplayName}' ending '{application.ApiKey.Substring(application.ApiKey.Length - 2)}'.");
        return application;
    }
}