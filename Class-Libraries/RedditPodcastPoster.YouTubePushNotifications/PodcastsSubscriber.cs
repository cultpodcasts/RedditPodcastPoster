using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PodcastsSubscriber(
    IPodcastRepository podcastRepository,
    IPodcastYouTubePushNotificationSubscriber subscriber,
    ILogger<PodcastsSubscriber> logger)
    : IPodcastsSubscriber
{
    public async Task SubscribePodcasts()
    {
        var podcasts =
            await podcastRepository
                .GetAllBy(x => x.IndexAllEpisodes && !string.IsNullOrWhiteSpace(x.YouTubeChannelId))
                .ToListAsync();
        var podcastsToSubscribe = podcasts
            .Where(podcastToSubscribe => string.IsNullOrWhiteSpace(podcastToSubscribe.YouTubePlaylistId) ||
                                         (!string.IsNullOrWhiteSpace(podcastToSubscribe.YouTubePlaylistId) &&
                                          !podcasts.Any(x =>
                                              x.YouTubeChannelId == podcastToSubscribe.YouTubeChannelId &&
                                              string.IsNullOrWhiteSpace(x.YouTubePlaylistId))))
            .Where(x =>
                x.YouTubeNotificationSubscriptionLeaseExpiry.HasValue &&
                x.YouTubeNotificationSubscriptionLeaseExpiry.Value < DateTime.UtcNow.AddDays(1));
        if (podcastsToSubscribe.Any())
        {
            logger.LogInformation(
                "Renewing leases for podcasts with ids {Join}.", string.Join((string?) ",", (IEnumerable<string?>) podcastsToSubscribe.Select(x => $"'{x.Id}'")));
            foreach (var podcastToSubscribe in podcastsToSubscribe)
            {
                logger.LogInformation("Renewing lease for podcast with id '{Guid}'.", podcastToSubscribe.Id);
                await subscriber.Renew(podcastToSubscribe);
            }
        }
        else
        {
            logger.LogInformation("No podcast's lease expiring in next 24 hours.");
        }
    }

    public async Task UpdateLease(Guid podcastId, long leaseSeconds)
    {
        logger.LogInformation(
            "Setting subscription-lease expiry for podcast with id '{PodcastId}' to {LeaseSeconds}s.", podcastId, leaseSeconds);
        var leaseExpiry = DateTime.UtcNow.AddSeconds(leaseSeconds);
        logger.LogInformation("Lease expires: {LeaseExpiry:O}", leaseExpiry);
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            var message = $"Unable to find podcast with id '{podcastId}'.";
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        podcast.YouTubeNotificationSubscriptionLeaseExpiry = leaseExpiry;
        await podcastRepository.Save(podcast);
    }

    public async Task RemoveLease(Guid podcastId)
    {
        logger.LogInformation(
            "Setting subscription-lease expiry for podcast with id '{PodcastId}' to null.", podcastId);
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            var message = $"Unable to find podcast with id '{podcastId}'.";
            logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        podcast.YouTubeNotificationSubscriptionLeaseExpiry = null;
        await podcastRepository.Save(podcast);
    }
}