﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace AddYouTubeChannelAsPodcast;

public class AddYouTubeChannelProcessor
{
    private readonly ILogger<AddYouTubeChannelProcessor> _logger;
    private readonly PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _repository;
    private readonly IYouTubeChannelResolver _youTubeChannelResolver;
    private readonly IYouTubeChannelService _youTubeChannelService;

    public AddYouTubeChannelProcessor(
        IYouTubeChannelResolver youTubeChannelResolver,
        IYouTubeChannelService youTubeChannelService,
        PodcastFactory podcastFactory,
        IPodcastRepository repository,
        ILogger<AddYouTubeChannelProcessor> logger)
    {
        _youTubeChannelResolver = youTubeChannelResolver;
        _youTubeChannelService = youTubeChannelService;
        _podcastFactory = podcastFactory;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Run(Args request)
    {
        var indexOptions = new IndexingContext();
        var match = await _youTubeChannelResolver.FindChannelsSnippets(
            request.ChannelName,
            request.MostRecentUploadedVideoTitle,
            indexOptions);
        if (match != null)
        {
            _logger.LogInformation($"Found channel-id: {match.Snippet.ChannelId}");
            var allPodcasts = await _repository.GetAll().ToListAsync();
            var matchingPodcast = allPodcasts.SingleOrDefault(x => x.YouTubeChannelId == match.Snippet.ChannelId);
            if (matchingPodcast != null)
            {
                _logger.LogError(
                    $"Found existing podcast with YouTube-Id '{match.Snippet.ChannelId}' with Podcast-id '{matchingPodcast.Id}'.");
                return false;
            }

            var channel =
                await _youTubeChannelService.GetChannelContentDetails(new YouTubeChannelId(match.Snippet.ChannelId),
                    indexOptions, withContentOwnerDetails: true);
            var newPodcast = _podcastFactory.Create(match.Snippet.ChannelTitle);
            newPodcast.Publisher = channel?.ContentOwnerDetails.ContentOwner ?? string.Empty;
            newPodcast.YouTubePublishingDelayTimeSpan = "0:00:00:00";
            newPodcast.YouTubeChannelId = match.Snippet.ChannelId;
            newPodcast.ReleaseAuthority = Service.YouTube;
            newPodcast.PrimaryPostService = Service.YouTube;
            await _repository.Save(newPodcast);
            _logger.LogInformation($"Created podcast with name '{newPodcast.Name}' and id '{newPodcast.Id}'.");
            return true;
        }

        _logger.LogError("Failed to match channel");
        return false;
    }
}