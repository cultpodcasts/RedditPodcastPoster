﻿using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeUrlCategoriser(
    IYouTubeChannelService youTubeChannelService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeUrlCategoriser> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IYouTubeUrlCategoriser
{
    private const int MultiplePublicationDateMatchTitleThreshold = 60;
    private const int TitleThreshold = 80;
    private static readonly TimeSpan PublishThreshold = TimeSpan.FromDays(2);

    public async Task<ResolvedYouTubeItem?> Resolve(
        Podcast? podcast,
        Uri url,
        IndexingContext indexingContext)
    {
        PodcastEpisode? pair = null;
        if (podcast != null && podcast.Episodes.Any(x => x.Urls.YouTube == url))
        {
            var storedEpisodes = podcast.Episodes.Where(x => x.Urls.YouTube == url);
            if (storedEpisodes.Count() > 1)
            {
                var ex = new InvalidOperationException(
                    $"Podcast '{podcast.Name}' with podcast-id '{podcast.Id}' has multiple episodes with url '{url}'.");
                logger.LogError(ex, ex.Message);
                throw ex;
            }

            var episode = storedEpisodes.Single();
            pair = new PodcastEpisode(podcast, episode);

            var episodes =
                await youTubeVideoService.GetVideoContentDetails(new[] {YouTubeIdResolver.Extract(url)!},
                    indexingContext, true);
            if (episodes != null && episodes.Any())
            {
                if (episodes.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple episodes retrieved from youtube video with url '{url}'.");
                }

                var description = episodes.First().Snippet.Description;
                if (pair.Episode.Description.Trim().EndsWith("...") &&
                    description.Length > pair.Episode.Description.Length)
                {
                    pair.Episode.Description = description;
                }
            }

            if (!string.IsNullOrWhiteSpace(pair.Podcast.YouTubeChannelId))
            {
                return new ResolvedYouTubeItem(pair);
            }
        }

        var videoId = YouTubeIdResolver.Extract(url);
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
                await youTubeChannelService.GetChannel(new YouTubeChannelId(item.Snippet.ChannelId),
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
                        item.GetLength() ?? TimeSpan.Zero,
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

    public async Task<ResolvedYouTubeItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        if (!string.IsNullOrWhiteSpace(matchingPodcast?.YouTubeChannelId))
        {
            if (matchingPodcast.Episodes.Any(x =>
                    (string.IsNullOrWhiteSpace(x.YouTubeId) && x.Urls.YouTube != null) ||
                    (x.Urls.YouTube == null && !string.IsNullOrWhiteSpace(x.YouTubeId)) ||
                    (!string.IsNullOrWhiteSpace(x.YouTubeId) && x.Urls.YouTube != null &&
                     YouTubeIdResolver.Extract(x.Urls.YouTube) != x.YouTubeId)
                ))
            {
                throw new InvalidOperationException(
                    $"Podcast with id '{matchingPodcast.Id}' has episodes with inconsistent youtube-id && youtube-url.");
            }

            var channelUploads =
                await youTubeChannelVideosService.GetChannelVideos(
                    new YouTubeChannelId(matchingPodcast.YouTubeChannelId), indexingContext);
            var unassignedChannelUploads = channelUploads.PlaylistItems.Where(x =>
                matchingPodcast.Episodes.Where(x => !string.IsNullOrWhiteSpace(x.YouTubeId)).Select(x => x.YouTubeId)
                    .Contains(x.Id));
            var expectedPublish = criteria.Release + matchingPodcast.YouTubePublishingDelay();
            var publishedWithin = unassignedChannelUploads.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset > expectedPublish.Subtract(PublishThreshold) &&
                x.Snippet.PublishedAtDateTimeOffset < expectedPublish.Add(PublishThreshold));
            PlaylistItem match;
            if (publishedWithin.Any())
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, publishedWithin, x => x.Snippet.ChannelTitle,
                    MultiplePublicationDateMatchTitleThreshold);
            }
            else
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, unassignedChannelUploads, x => x.Snippet.ChannelTitle,
                    TitleThreshold);
            }

            if (match != null)
            {
                var video = await youTubeVideoService.GetVideoContentDetails([match.Id], indexingContext);
                return new ResolvedYouTubeItem(
                    match.Snippet.ChannelId,
                    match.Id,
                    match.Snippet.ChannelTitle,
                    channelUploads.Channel.Snippet.Description,
                    channelUploads.Channel.ContentOwnerDetails.ContentOwner,
                    match.Snippet.Title,
                    match.Snippet.Description,
                    match.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                    video?.SingleOrDefault()?.GetLength() ?? TimeSpan.Zero,
                    match.Snippet.ToYouTubeUrl(),
                    video?.SingleOrDefault()?.ContentDetails.ContentRating.YtRating == "ytAgeRestricted");
            }
        }
        else
        {
            if (matchingPodcast != null)
            {
                logger.LogInformation(
                    $"Podcast with id '{matchingPodcast.YouTubeChannelId}' does not have youtube-id.");
            }

            return null;
        }

        return null;
    }
}