using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PodcastsSubscriber : IPodcastsSubscriber
{
    private readonly ILogger<PodcastsSubscriber> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastYouTubePushNotificationSubscriber _subscriber;

    public PodcastsSubscriber(
        IPodcastRepository podcastRepository,
        IPodcastYouTubePushNotificationSubscriber subscriber,
        ILogger<PodcastsSubscriber> logger)
    {
        _podcastRepository = podcastRepository;
        _subscriber = subscriber;
        _logger = logger;
    }

    public async Task SubscribePodcasts()
    {
        var podcasts =
            await _podcastRepository
                .GetAll()
                .Where(x => !string.IsNullOrEmpty(x.YouTubeChannelId))
                .ToListAsync();
        var podcastsToSubscribe =
            podcasts
                .Where(podcastToSubscribe => string.IsNullOrEmpty(podcastToSubscribe.YouTubePlaylistId) ||
                                             (!string.IsNullOrEmpty(podcastToSubscribe.YouTubePlaylistId) &&
                                              !podcasts.Any(x =>
                                                  x.YouTubeChannelId == podcastToSubscribe.YouTubeChannelId &&
                                                  string.IsNullOrEmpty(x.YouTubePlaylistId))))
                .Where(x => !x.YouTubeNotificationSubscriptionLeaseExpiry.HasValue ||
                            x.YouTubeNotificationSubscriptionLeaseExpiry.Value < DateTime.UtcNow.AddDays(1));
        if (podcastsToSubscribe.Any())
        {
            _logger.LogInformation(
                $"Renewing leases for podcasts with ids {string.Join((string?) ",", (IEnumerable<string?>) podcastsToSubscribe.Select(x => $"'{x.Id}'"))}.");
            foreach (var podcastToSubscribe in podcastsToSubscribe)
            {
                _logger.LogInformation($"Renewing lease for podcast with id '{podcastToSubscribe.Id}'.");
                await _subscriber.Renew(podcastToSubscribe);
            }
        }
        else
        {
            _logger.LogInformation("No podcast's lease expiring in next 24 hours.");
        }
    }

    public async Task UpdateLease(Guid podcastId, long leaseSeconds)
    {
        _logger.LogInformation(
            $"Setting subscription-lease expiry for podcast with id '{podcastId}' to {leaseSeconds}s.");
        var leaseExpiry = DateTime.UtcNow.AddSeconds(leaseSeconds);
        _logger.LogInformation($"Lease expires: {leaseExpiry:O}");
        var podcast = await _podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            var message = $"Unable to find podcast with id '{podcastId}'.";
            _logger.LogError(message);
            throw new InvalidOperationException(message);
        }

        podcast.YouTubeNotificationSubscriptionLeaseExpiry = leaseExpiry;
        await _podcastRepository.Save(podcast);
    }
}