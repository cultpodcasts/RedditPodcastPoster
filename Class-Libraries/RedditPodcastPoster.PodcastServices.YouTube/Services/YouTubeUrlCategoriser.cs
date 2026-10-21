using Microsoft.Extensions.Logging;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;
using Google.Apis.YouTube.v3.Data;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using PodcastEpisode = RedditPodcastPoster.Models.Episodes.PodcastEpisode;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class YouTubeUrlCategoriser(
    IYouTubeServiceWrapper youTubeService,
    ITolerantYouTubeChannelService youTubeChannelService,
    ITolerantYouTubeVideoService youTubeVideoService,
    IYouTubeChannelReleaseBandSearch youTubeChannelReleaseBandSearch,
    IYouTubeChannelVideoRetrievalPolicy youTubeChannelVideoRetrievalPolicy,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    ILogger<YouTubeUrlCategoriser> logger)
    : IYouTubeUrlCategoriser
{
    private const int MultiplePublicationDateMatchTitleThreshold = 60;
    private const int TitleThreshold = 80;
    private const int SameTitleThreshold = 95;
    private static readonly long SameTitleDurationThreshold = TimeSpan.FromMinutes(2).Ticks;
    private static readonly TimeSpan PublishThreshold = TimeSpan.FromDays(2);

    public async Task<ResolvedYouTubeItem?> Resolve(
        Podcast? podcast,
        IList<EpisodeModel> podcastEpisodes,
        Uri url,
        IndexingContext indexingContext)
    {
        PodcastEpisode? pair = null;
        if (podcast != null && podcastEpisodes.Any(x =>
                EpisodeServicePresence.TryGetUrl(x, ServiceKeys.YouTube) == url))
        {
            var storedEpisodes = podcastEpisodes.Where(x =>
                EpisodeServicePresence.TryGetUrl(x, ServiceKeys.YouTube) == url).ToArray();
            if (storedEpisodes.Length > 1)
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

            var channel = await youTubeChannelService.GetChannel(new YouTubeChannelId(item.Snippet.ChannelId),
                indexingContext, true, true);
            var snippetChannelTitle = item.Snippet.ChannelTitle;
            var snippetDescription = channel!.Snippet.Description;

            var playlistId = YouTubePlaylistIdResolver.Extract(url);
            if (!string.IsNullOrWhiteSpace(playlistId))
            {
                var playlist = await youTubePlaylistService.GetPlaylistInfo(
                    new YouTubePlaylistId(playlistId, YouTubePlaylistIdSource.Unknown, "categoriser"), indexingContext);
                snippetChannelTitle = playlist.Title;
                snippetDescription = playlist.Description;
            }

            if (channel != null)
            {
                if (pair == null)
                {
                    return new ResolvedYouTubeItem(
                        item.Snippet.ChannelId,
                        item.Id,
                        snippetChannelTitle,
                        snippetDescription,
                        channel.ContentOwnerDetails.ContentOwner,
                        item.Snippet.Title,
                        item.Snippet.Description,
                        item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        item.GetLength() ?? TimeSpan.Zero,
                        item.ToYouTubeUrl(),
                        item.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        await youTubeThumbnailResolver.GetImageUrlAsync(item),
                        playlistId
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
        IList<EpisodeModel> episodes,
        IndexingContext indexingContext)
    {
        if (!string.IsNullOrWhiteSpace(matchingPodcast?.YouTubeChannelId))
        {
            string channelDescription = "", channelContentOwner = "";
            var mismatchedEpisodes = episodes.Where(HasInconsistentYouTubeIdAndUrl).ToArray();
            if (mismatchedEpisodes.Any())
            {
                throw new InvalidOperationException(
                    $"Podcast with id '{matchingPodcast.Id}' has episodes with inconsistent youtube-id && youtube-url. Episode-ids: {string.Join(", ", mismatchedEpisodes.Select(x => x.Id))}");
            }

            var expectedPublish = criteria.Release.Add(matchingPodcast.YouTubePublishingDelay());
            var band = EpisodeReleaseTolerance.GetSubmitMatchBand(expectedPublish);
            var publishedAfter = new DateTimeOffset(DateTime.SpecifyKind(band.Start, DateTimeKind.Utc));
            var publishedBefore = new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc));
            var items = await LoadSubmitMatchCandidates(
                matchingPodcast,
                indexingContext,
                publishedAfter,
                publishedBefore);

            if (items == null || !items.Any())
            {
                return null;
            }

            var podcastEpisodeYouTubeIds = episodes
                .Select(x => EpisodeServicePresence.YouTubeEpisodeId(x))
                .Where(id => !string.IsNullOrWhiteSpace(id));
            var unassignedChannelUploads =
                items.Where(x => !podcastEpisodeYouTubeIds.Contains(x.Id)).ToArray();
            var publishedWithin = unassignedChannelUploads.Where(x =>
                    x.Snippet.PublishedAtDateTimeOffset > expectedPublish.Subtract(PublishThreshold) &&
                    x.Snippet.PublishedAtDateTimeOffset < expectedPublish.Add(PublishThreshold))
                .ToArray();
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
                    if (videoContent is { Count: 1 })
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
                var channel = await youTubeChannelService.GetChannel(
                    new YouTubeChannelId(match.Snippet.ChannelId),
                    indexingContext,
                    true,
                    true);
                if (channel != null)
                {
                    channelDescription = channel.Snippet.Description;
                    channelContentOwner = channel.ContentOwnerDetails.ContentOwner;
                }

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
                        channelDescription, //
                        channelContentOwner, //
                        match.Snippet.Title,
                        match.Snippet.Description,
                        match.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        videoContent?.GetLength() ?? TimeSpan.Zero,
                        match.Snippet.ToYouTubeUrl(),
                        videoContent?.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        videoContent != null
                            ? await youTubeThumbnailResolver.GetImageUrlAsync(videoContent)
                            : null,
                        string.Empty);
                }
            }
        }
        else
        {
            if (matchingPodcast != null)
            {
                logger.LogInformation("Podcast with id '{podcastId}' does not have youtube-id.", matchingPodcast.Id);
            }
        }

        return null;
    }

    private async Task<IList<PlaylistItem>?> LoadSubmitMatchCandidates(
        Podcast matchingPodcast,
        IndexingContext indexingContext,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore)
    {
        var uploadsPlaylistReason = youTubeChannelVideoRetrievalPolicy.GetUploadsPlaylistReason(matchingPodcast);
        if (uploadsPlaylistReason != null)
        {
            return await LoadBandBoundedPlaylist(
                matchingPodcast, indexingContext, publishedAfter, publishedBefore, uploadsPlaylistReason);
        }

        try
        {
            var bandResults = await youTubeChannelReleaseBandSearch.Search(
                matchingPodcast.YouTubeChannelId,
                publishedAfter,
                publishedBefore,
                indexingContext);
            return bandResults?
                .Where(x => x.Id?.VideoId != null && x.Snippet != null)
                .Select(x => new PlaylistItem
                {
                    Id = x.Id.VideoId,
                    Snippet = new PlaylistItemSnippet
                    {
                        Title = x.Snippet.Title,
                        Description = x.Snippet.Description,
                        ChannelId = x.Snippet.ChannelId,
                        ChannelTitle = x.Snippet.ChannelTitle,
                        PublishedAtDateTimeOffset = x.Snippet.PublishedAtDateTimeOffset,
                        ResourceId = new ResourceId { VideoId = x.Id.VideoId }
                    }
                })
                .ToList();
        }
        catch (YouTubeChannelSearchForbiddenException ex)
        {
            logger.LogInformation(ex,
                "Search.List is not permitted for channel-id '{ChannelId}'; falling back to the playlist.",
                matchingPodcast.YouTubeChannelId);
            matchingPodcast.YouTubeChannelSearchForbidden = true;
            return await LoadBandBoundedPlaylist(
                matchingPodcast,
                indexingContext,
                publishedAfter,
                publishedBefore,
                "youTubeChannelSearchForbidden");
        }
    }

    /// <summary>
    /// Playlist and uploads walks have a floor and no ceiling. The floor is the band start so paging
    /// stops once publish times are older than it. A years-old band still pages back to that date.
    /// Items newer than the band end are dropped here.
    /// </summary>
    private async Task<IList<PlaylistItem>?> LoadBandBoundedPlaylist(
        Podcast matchingPodcast,
        IndexingContext indexingContext,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore,
        string reason)
    {
        logger.LogInformation(
            "Submit match paging playlist until publish times are older than {BandStart:u} for channel '{ChannelId}' ({Reason}). Items newer than {BandEnd:u} are dropped.",
            publishedAfter,
            matchingPodcast.YouTubeChannelId,
            reason,
            publishedBefore);

        var playlistId = await ResolveFallbackPlaylistId(matchingPodcast, indexingContext);
        if (playlistId == null)
        {
            return null;
        }

        var playlistContext = indexingContext with { ReleasedSince = publishedAfter.UtcDateTime };
        var response = await youTubePlaylistService.GetPlaylistVideoSnippets(
            playlistId,
            playlistContext,
            expensivePlaylist: matchingPodcast.HasExpensiveYouTubePlaylistQuery());
        return WithinSubmitMatchBand(response.Result, publishedAfter, publishedBefore);
    }

    private async Task<YouTubePlaylistId?> ResolveFallbackPlaylistId(
        Podcast matchingPodcast,
        IndexingContext indexingContext)
    {
        if (!string.IsNullOrWhiteSpace(matchingPodcast.YouTubePlaylistId))
        {
            return new YouTubePlaylistId(
                matchingPodcast.YouTubePlaylistId,
                YouTubePlaylistIdSource.PodcastEntity,
                matchingPodcast.Id.ToString());
        }

        var channel = await youTubeChannelService.GetChannel(
            new YouTubeChannelId(matchingPodcast.YouTubeChannelId),
            indexingContext,
            withContentDetails: true);
        var uploadsId = channel?.ContentDetails?.RelatedPlaylists?.Uploads;
        if (string.IsNullOrWhiteSpace(uploadsId))
        {
            logger.LogInformation(
                "No uploads playlist for channel-id '{ChannelId}'.",
                matchingPodcast.YouTubeChannelId);
            return null;
        }

        return new YouTubePlaylistId(
            uploadsId,
            YouTubePlaylistIdSource.ChannelUploads,
            matchingPodcast.YouTubeChannelId);
    }

    private static IList<PlaylistItem>? WithinSubmitMatchBand(
        IList<PlaylistItem>? items,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore)
    {
        if (items == null)
        {
            return null;
        }

        return items.Where(item =>
        {
            var published = item.ContentDetails?.VideoPublishedAtDateTimeOffset
                            ?? item.Snippet?.PublishedAtDateTimeOffset;
            return published >= publishedAfter && published < publishedBefore;
        }).ToList();
    }

    /// <summary>
    /// True when YouTube id and URL disagree, or only one of them is present
    /// (except a removed episode that has a URL and no id).
    /// </summary>
    internal static bool HasInconsistentYouTubeIdAndUrl(EpisodeModel episode)
    {
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode);
        var youTubeUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube);
        var idMissing = string.IsNullOrWhiteSpace(youTubeId);
        return (!episode.IsRemoved() && idMissing && youTubeUrl is not null) ||
               (youTubeUrl is null && !idMissing) ||
               (!idMissing && youTubeUrl is not null &&
                YouTubeIdResolver.Extract(youTubeUrl) != youTubeId);
    }
}
