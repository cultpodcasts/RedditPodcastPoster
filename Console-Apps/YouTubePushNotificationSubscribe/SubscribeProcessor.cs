﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.YouTubePushNotifications;
using YouTubePushNotificationSubcribe;

namespace YouTubePushNotificationSubscribe;

public class SubscribeProcessor
{
    private readonly ILogger<SubscribeProcessor> _logger;
    private readonly IPodcastsSubscriber _podcastsSubscriber;
    private readonly IPodcastRepository _repository;
    private readonly IPodcastYouTubePushNotificationSubscriber _subscriber;

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

            _logger.LogInformation($"Subscribing podcast with id '{request.PodcastId}'.");
            await _subscriber.Renew(podcast);
        }
        else if (request.UnsubscribePodcastId.HasValue)
        {
            var podcast = await _repository.GetPodcast(request.UnsubscribePodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.UnsubscribePodcastId}' not found.");
            }

            _logger.LogInformation($"Unsubscribing podcast with id '{request.UnsubscribePodcastId}'.");
            await _subscriber.Unsubscribe(podcast);
        }
        else if (request.RenewAllLeases)
        {
            _logger.LogInformation($"Renewing all leases.");
            await _podcastsSubscriber.SubscribePodcasts();
        }
        else
        {
            throw new ArgumentException("Unknown operation");
        }
    }
}