using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class YouTubeUrlCategoriser(
    IYouTubeServiceWrapper youTubeService,
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
    private const int SameTitleThreshold = 95;
    private static readonly long SameTitleDurationThreshold = TimeSpan.FromMinutes(2).Ticks;
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
                await youTubeVideoService.GetVideoContentDetails(youTubeService, [YouTubeIdResolver.Extract(url)!],
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

        var items = await youTubeVideoService.GetVideoContentDetails(youTubeService, [videoId], indexingContext, true);
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
                        channel.Snippet.Description,
                        channel.ContentOwnerDetails.ContentOwner,
                        item.Snippet.Title,
                        item.Snippet.Description,
                        item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        item.GetLength() ?? TimeSpan.Zero,
                        item.ToYouTubeUrl(),
                        item.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        item.GetImageUrl()
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
            var mismatchedEpisodes = matchingPodcast.Episodes.Where(x =>
                (!x.Removed &&
                 string.IsNullOrWhiteSpace(x.YouTubeId) && x.Urls.YouTube != null) ||
                (x.Urls.YouTube == null && !string.IsNullOrWhiteSpace(x.YouTubeId)) ||
                (!string.IsNullOrWhiteSpace(x.YouTubeId) && x.Urls.YouTube != null &&
                 YouTubeIdResolver.Extract(x.Urls.YouTube) != x.YouTubeId)
            );
            if (mismatchedEpisodes.Any())
            {
                throw new InvalidOperationException(
                    $"Podcast with id '{matchingPodcast.Id}' has episodes with inconsistent youtube-id && youtube-url. Episode-ids: {string.Join(", ", mismatchedEpisodes.Select(x => x.Id))}");
            }

            var podcastEpisodeYouTubeIds = matchingPodcast.Episodes.Where(y => !string.IsNullOrWhiteSpace(y.YouTubeId))
                .Select(x => x.YouTubeId);

            Models.ChannelVideos? channelVideos;
            if (string.IsNullOrWhiteSpace(matchingPodcast.YouTubePlaylistId))
            {
                channelVideos = await youTubeChannelVideosService.GetChannelVideos(
                    new YouTubeChannelId(matchingPodcast.YouTubeChannelId), indexingContext);
            }
            else
            {
                channelVideos = await youTubeChannelVideosService.GetPlaylistVideos(
                    new YouTubeChannelId(matchingPodcast.YouTubeChannelId),
                    new YouTubePlaylistId(matchingPodcast.YouTubePlaylistId),
                    indexingContext);
            }

            var unassignedChannelUploads =
                channelVideos.PlaylistItems.Where(x => !podcastEpisodeYouTubeIds.Contains(x.Id));
            var expectedPublish = criteria.Release + matchingPodcast.YouTubePublishingDelay();
            var publishedWithin = unassignedChannelUploads.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset > expectedPublish.Subtract(PublishThreshold) &&
                x.Snippet.PublishedAtDateTimeOffset < expectedPublish.Add(PublishThreshold));
            PlaylistItem? match;
            if (publishedWithin.Any())
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, publishedWithin, x => x.Snippet.Title,
                    MultiplePublicationDateMatchTitleThreshold);
            }
            else
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, unassignedChannelUploads, x => x.Snippet.Title,
                    TitleThreshold);
            }

            if (match == null)
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, unassignedChannelUploads, x => x.Snippet.Title,
                    SameTitleThreshold);
                if (match != null)
                {
                    var videoContent =
                        await youTubeVideoService.GetVideoContentDetails(
                            youTubeService,
                            [match.Snippet.ResourceId.VideoId],
                            indexingContext
                        );
                    if (videoContent is {Count: 1})
                    {
                        var duration = videoContent.Single().GetLength();
                        if (duration.HasValue)
                        {
                            var diff = Math.Abs((duration.Value - criteria.Duration).Ticks);
                            if (diff > SameTitleDurationThreshold)
                            {
                                match = null;
                            }
                        }
                        else
                        {
                            match = null;
                        }
                    }
                    else
                    {
                        match = null;
                    }
                }
            }

            if (match != null)
            {
                var video = await youTubeVideoService.GetVideoContentDetails(youTubeService,
                    [match.Snippet.ResourceId.VideoId],
                    indexingContext,
                    true);
                if (video != null)
                {
                    var videoContent = video.SingleOrDefault();
                    return new ResolvedYouTubeItem(
                        match.Snippet.ChannelId,
                        match.Snippet.ResourceId.VideoId,
                        match.Snippet.ChannelTitle,
                        channelVideos.Channel.Snippet.Description, //
                        channelVideos.Channel.ContentOwnerDetails.ContentOwner, //
                        match.Snippet.Title,
                        match.Snippet.Description,
                        match.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        videoContent?.GetLength() ?? TimeSpan.Zero,
                        match.Snippet.ToYouTubeUrl(),
                        videoContent?.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        videoContent?.GetImageUrl());
                }
            }
        }
        else
        {
            if (matchingPodcast != null)
            {
                logger.LogInformation("Podcast with id '{youTubeChannelId}' does not have youtube-id.",
                    matchingPodcast.YouTubeChannelId);
            }
        }

        return null;
    }
}