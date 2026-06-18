using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Strategies;

public class YouTubeApiKeyStrategy(
    IOptions<YouTubeSettings> settings,
    IDateTimeService dateTimeService,
    ILogger<YouTubeApiKeyStrategy> logger)
    : IYouTubeApiKeyStrategy
{
    private readonly YouTubeSettings _settings =
        settings.Value ?? throw new ArgumentNullException($"Missing {nameof(YouTubeSettings)}.");

    public ApplicationWrapper GetApplication(ApplicationUsage usage)
    {
        var usageApplications =
            _settings.Applications.Where(x => MatchesUsage(x, usage) && x.Reattempt == null).ToArray();
        var settingsCount = usageApplications.Length;
        if (settingsCount == 0)
        {
            throw new InvalidOperationException($"No youtube-applications registered or usage '{usage.ToString()}'");
        }

        var applicationIndex = dateTimeService.GetHour() / (24 / settingsCount);
        var application = usageApplications.Skip(applicationIndex).First();
        logger.LogInformation(
            "{methodName}: Using application-key for {usage} with name '{displayName}' ({position}/{settingsCount}) ending '{keyEnding}'.",
            nameof(GetApplication), usage.ToString(), application.DisplayName, applicationIndex + 1, settingsCount,
            application.ApiKey.Substring(application.ApiKey.Length - 2));
        return new ApplicationWrapper(
            application,
            applicationIndex,
            _settings.Applications.Where(x => MatchesUsage(x, usage)).Max(x => x.Reattempt) ?? 0
        );
    }

    public ApplicationWrapper GetApplication(ApplicationUsage usage, int index, int reattempt)
    {
        logger.LogInformation("{method}: usage= '{usage}', index= {index}, reattempt= {reattempt}",
            nameof(GetApplication), usage, index, reattempt);
        var usageApplications =
            _settings.Applications.Where(x => MatchesUsage(x, usage) && x.Reattempt == reattempt).ToArray();
        var settingsCount = usageApplications.Length;
        if (settingsCount == 0)
        {
            throw new InvalidOperationException(
                $"No youtube-applications registered for usage '{usage.ToString()}' with reattempt '{reattempt}' (Index-requested: '{index}').");
        }

        if (settingsCount <= index)
        {
            throw new InvalidOperationException(
                $"Inadequate number of youtube-applications registered for usage '{usage.ToString()}'. Applications: '{settingsCount}', Index-requested: '{index}'.");
        }

        var application = usageApplications.Skip(index).First();
        logger.LogInformation(
            "{methodName}: Using application-key for {usage} with name '{displayName}' ({position}/{settingsCount}) ending '{keyEnding}' for reattempt {reattempt}.",
            nameof(GetApplication), usage.ToString(), application.DisplayName, index + 1, settingsCount,
            application.ApiKey.Substring(application.ApiKey.Length - 2), reattempt);
        return new ApplicationWrapper(
            application,
            index,
            _settings.Applications.Where(x => MatchesUsage(x, usage)).Max(x => x.Reattempt) ?? 0);
    }

    public IReadOnlyList<ApplicationWrapper> BuildIndexerKeyRing(int startPrimaryIndex)
    {
        var ring = IndexerKeyRingBuilder.Build(_settings.Applications, startPrimaryIndex);
        logger.LogInformation(
            "{methodName}: Built indexer key ring with {count} unique keys starting at primary index {startPrimaryIndex}. Order: {order}.",
            nameof(BuildIndexerKeyRing),
            ring.Count,
            startPrimaryIndex,
            string.Join(" -> ", ring.Select(x => x.Application.DisplayName)));
        return ring;
    }

    private static bool MatchesUsage(Application application, ApplicationUsage usage) =>
        usage switch
        {
            ApplicationUsage.Indexer => application.Usage == ApplicationUsage.Indexer,
            ApplicationUsage.Discover => application.Usage == ApplicationUsage.Discover,
            ApplicationUsage.Api => application.Usage == ApplicationUsage.Api,
            ApplicationUsage.Bluesky => application.Usage == ApplicationUsage.Bluesky,
            ApplicationUsage.Cli => application.Usage == ApplicationUsage.Cli,
            _ => application.Usage.HasFlag(usage)
        };
}
