﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeUrlCategoriser(
    IYouTubeChannelService youTubeChannelService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeIdExtractor youTubeIdExtractor,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeUrlCategoriser> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IYouTubeUrlCategoriser
{
    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("youtube");
    }

    public async Task<ResolvedYouTubeItem?> Resolve(
        Podcast? podcast,
        Uri url,
        IndexingContext indexingContext)
    {
        PodcastEpisode? pair = null;
        if (podcast != null && podcast.Episodes.Any(x => x.Urls.YouTube == url))
        {
            pair = new PodcastEpisode(podcast,
                podcast.Episodes.Single(x => x.Urls.YouTube == url));
            if (!string.IsNullOrWhiteSpace(pair.Podcast.YouTubeChannelId))
            {
                return new ResolvedYouTubeItem(pair);
            }
        }

        var videoId = youTubeIdExtractor.Extract(url);
        if (videoId == null)
        {
            throw new InvalidOperationException($"Unable to find video-id in url '{url}'.");
        }

        var items = await youTubeVideoService.GetVideoContentDetails(new[] {videoId}, indexingContext, true);
        if (items != null)
        {
            var item = items.FirstOrDefault();
            if (item == null)
            {
                throw new InvalidOperationException($"Unable to find video with id '{videoId}'.");
            }

            var channel =
                await youTubeChannelService.GetChannelContentDetails(new YouTubeChannelId(item.Snippet.ChannelId),
                    indexingContext, true, true);
            if (channel != null)
            {
                if (pair == null)
                {
                    return new ResolvedYouTubeItem(
                        item.Snippet.ChannelId,
                        item.Id,
                        item.Snippet.ChannelTitle,
                        channel!.Snippet.Description,
                        channel.ContentOwnerDetails.ContentOwner,
                        item.Snippet.Title,
                        item.Snippet.Description,
                        item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        item.GetLength(),
                        item.ToYouTubeUrl(),
                        item.ContentDetails.ContentRating.YtRating == "ytAgeRestricted"
                    );
                }

                if (string.IsNullOrWhiteSpace(pair.Podcast.YouTubeChannelId))
                {
                    pair.Podcast.YouTubeChannelId = item.Snippet.ChannelId;
                    return new ResolvedYouTubeItem(pair);
                }
            }
        }
        else
        {
            if (indexingContext.SkipYouTubeUrlResolving)
            {
                throw new InvalidOperationException(
                    $"Error: {nameof(indexingContext.SkipYouTubeUrlResolving)} be true.");
            }
        }

        return null;
    }

    public Task<ResolvedYouTubeItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        return Task.FromResult((ResolvedYouTubeItem) null!)!;
    }
}