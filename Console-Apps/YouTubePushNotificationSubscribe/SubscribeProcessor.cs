using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.YouTubePushNotifications;
using YouTubePushNotificationSubcribe;

namespace YouTubePushNotificationSubscribe;

public class SubscribeProcessor(
    IPodcastRepository repository,
    IPodcastsSubscriber podcastsSubscriber,
    IPodcastYouTubePushNotificationSubscriber subscriber,
    ILogger<SubscribeProcessor> logger)
{
    public async Task Process(SubscribeRequest request)
    {
        if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId}' not found.");
            }

            logger.LogInformation("Subscribing podcast with id '{RequestPodcastId}'.", request.PodcastId);
            await subscriber.Renew(podcast);
        }
        else if (request.UnsubscribePodcastId.HasValue)
        {
            var podcast = await repository.GetPodcast(request.UnsubscribePodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.UnsubscribePodcastId}' not found.");
            }

            logger.LogInformation("Unsubscribing podcast with id '{RequestUnsubscribePodcastId}'.", request.UnsubscribePodcastId);
            await subscriber.Unsubscribe(podcast);
        }
        else if (request.RenewAllLeases)
        {
            logger.LogInformation("Renewing all leases.");
            await podcastsSubscriber.SubscribePodcasts();
        }
        else
        {
            throw new ArgumentException("Unknown operation");
        }
    }
}