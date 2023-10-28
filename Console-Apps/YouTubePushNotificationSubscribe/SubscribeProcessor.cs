using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.YouTubePushNotifications;
using YouTubePushNotificationSubcribe;

namespace YouTubePushNotificationSubscribe;

public class SubscribeProcessor
{
    private readonly IPodcastRepository _repository;
    private readonly IPodcastsSubscriber _podcastsSubscriber;
    private readonly IPodcastYouTubePushNotificationSubscriber _subscriber;
    private readonly ILogger<SubscribeProcessor> _logger;

    public SubscribeProcessor(
        IPodcastRepository repository,
        IPodcastsSubscriber podcastsSubscriber,
        IPodcastYouTubePushNotificationSubscriber subscriber,
        ILogger<SubscribeProcessor> logger)
    {
        _repository = repository;
        _podcastsSubscriber = podcastsSubscriber;
        _subscriber = subscriber;
        _logger = logger;
    }

    public async Task Process(SubscribeRequest request)
    {
        if (request.PodcastId.HasValue)
        {
            var podcast = await _repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId}' not found.");
            }

            await _subscriber.Renew(podcast);
        }
        else
        {
            await _podcastsSubscriber.SubscribePodcasts();
        }

    }
}