using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Mapping;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Episode;

public class YouTubeEpisodeProvider(
    IYouTubeServiceWrapper youTubeService,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    ITolerantYouTubeVideoService youTubeVideoService,
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
    IYouTubeChannelVideoRetrievalPolicy youTubeChannelVideoRetrievalPolicy,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IEpisodeCatalogueAdapter<YouTubeCatalogueInput> youTubeEpisodeAdapter,
    IEpisodeFromCandidateFactory episodeFromCandidateFactory,
    ILogger<YouTubeEpisodeProvider> logger)
    : IYouTubeEpisodeProvider
{
    public async Task<IList<EpisodeModel>?> GetEpisodes(
        Podcast podcast,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds)
    {
        var channelId = new YouTubeChannelId(podcast.YouTubeChannelId);
        var uploadsPlaylistReason = youTubeChannelVideoRetrievalPolicy.GetUploadsPlaylistReason(podcast);
        if (uploadsPlaylistReason != null)
        {
            logger.LogInformation(
                "Using channel uploads playlist for channel-id '{ChannelId}' ({Reason}).",
                channelId.ChannelId, uploadsPlaylistReason);
            return await GetEpisodesFromChannelUploadsPlaylist(podcast, channelId, indexingContext, knownIds);
        }

        try
        {
            var youTubeVideos =
                await youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(channelId, indexingContext);
            if (youTubeVideos != null)
            {
                var youTubeVideoIds =
                    youTubeVideos.Select(x => x.Id.VideoId).Where(x => !knownIds.Contains(x)).ToArray();

                if (youTubeVideoIds.Any())
                {
                    var videoDetails =
                        await youTubeVideoService.GetVideoContentDetails(youTubeService, youTubeVideoIds,
                            indexingContext,
                            withSnippets: true,
                            withStatistics: true);

                    if (videoDetails != null)
                    {
                        var episodes = new List<EpisodeModel>();
                        foreach (var videoDetail in videoDetails.Where(videoDetail =>
                                     YouTubeVideoDurationMatcher.HasDuration(videoDetail.GetLength())))
                        {
                            if (SkipMembersOnly(videoDetail))
                            {
                                continue;
                            }

                            episodes.Add(await GetEpisodeAsync(
                                youTubeVideos.First(searchResult => searchResult.Id.VideoId == videoDetail.Id),
                                videoDetail));
                        }

                        return episodes;
                    }
                }
            }

            return null;
        }
        catch (YouTubeChannelSearchForbiddenException ex)
        {
            logger.LogInformation(ex,
                "Search.List is not permitted for channel-id '{ChannelId}'; falling back to channel uploads playlist.",
                channelId.ChannelId);
            podcast.YouTubeChannelSearchForbidden = true;
            return await GetEpisodesFromChannelUploadsPlaylist(podcast, channelId, indexingContext, knownIds);
        }
    }

    private async Task<IList<EpisodeModel>?> GetEpisodesFromChannelUploadsPlaylist(
        Podcast podcast,
        YouTubeChannelId request,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds)
    {
        var channelVideosResponse = await youTubeChannelVideosService.GetChannelVideos(request, indexingContext);
        if (channelVideosResponse.PlaylistItems == null)
        {
            if (channelVideosResponse.Failure != null)
            {
                logger.LogPlaylistFetchFailure(podcast, channelVideosResponse.Failure,
                    new YouTubePlaylistId(
                        channelVideosResponse.Channel?.ContentDetails?.RelatedPlaylists?.Uploads ?? string.Empty,
                        YouTubePlaylistIdSource.ChannelUploads,
                        request.ChannelId));
            }

            return null;
        }

        var playlistItems = channelVideosResponse.PlaylistItems
            .Where(x => !knownIds.Contains(x.Snippet.ResourceId.VideoId))
            .ToList();
        if (indexingContext.ReleasedSince.HasValue)
        {
            playlistItems = playlistItems
                .Where(x => x.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince))
                .ToList();
        }

        if (!playlistItems.Any())
        {
            return null;
        }

        var videoDetails = await youTubeVideoService.GetVideoContentDetails(
            youTubeService,
            playlistItems.Select(x => x.Snippet.ResourceId.VideoId),
            indexingContext,
            withSnippets: true,
            withStatistics: true);
        if (videoDetails == null || !videoDetails.Any())
        {
            return null;
        }

        var episodes = new List<EpisodeModel>();
        foreach (var playlistItemVideo in playlistItems
                     .Where(x => x.Snippet.Title != "Deleted video")
                     .Select(x =>
                         new PlaylistItemVideo(
                             x,
                             videoDetails.SingleOrDefault(videoDetail => videoDetail.Id == x.Snippet.ResourceId.VideoId)!))
                     .Where(x => x.VideoDetails?.Snippet != null)
                     .Where(x => YouTubeVideoDurationMatcher.HasDuration(x.VideoDetails.GetLength()))
                     .Where(x => x.VideoDetails.IsCompletedPublicVideo())
                     .Where(x => x.VideoDetails.Snippet.ChannelId == request.ChannelId))
        {
            if (SkipMembersOnly(playlistItemVideo.VideoDetails))
            {
                continue;
            }

            episodes.Add(await BuildEpisodeAsync(
                playlistItemVideo.PlaylistItem.Snippet, playlistItemVideo.VideoDetails, indexingContext));
        }

        return episodes;
    }

    public async Task<EpisodeModel> GetEpisodeAsync(SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails)
    {
        var image = await youTubeThumbnailResolver.GetImageUrlAsync(videoDetails);
        var candidate = youTubeEpisodeAdapter.Adapt(
            searchResult.ToCatalogueInput(videoDetails, image));
        var isExplicit = videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted";
        return episodeFromCandidateFactory.Create(candidate, isExplicit);
    }

    public Task<EpisodeModel> GetEpisodeAsync(PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails)
    {
        return BuildEpisodeAsync(playlistItemSnippet, videoDetails, null);
    }

    private async Task<EpisodeModel> BuildEpisodeAsync(PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails,
        IndexingContext? indexingContext)
    {
        indexingContext?.ReportStage("youtube-thumbnail", $"video-id={videoDetails.Id}");
        var image = await youTubeThumbnailResolver.GetImageUrlAsync(
            videoDetails,
            indexingContext?.CancellationToken ?? CancellationToken.None);
        var candidate = youTubeEpisodeAdapter.Adapt(
            playlistItemSnippet.ToCatalogueInput(videoDetails, image));
        var isExplicit = videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted";
        return episodeFromCandidateFactory.Create(candidate, isExplicit);
    }

    public async Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(Podcast podcast, YouTubePlaylistId youTubePlaylistId,
        YouTubeChannelId? youTubeChannelId, IndexingContext indexingContext, bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null, IEnumerable<string>? knownIds = null)
    {
        var playlistQueryResponse = await youTubePlaylistService.GetPlaylistVideoSnippets(
            youTubePlaylistId, indexingContext,
            expensivePlaylist: expensivePlaylist, playlistOrder: playlistOrder);
        var isExpensiveQuery = playlistQueryResponse.IsExpensiveQuery;
        if (playlistQueryResponse.Result == null || !playlistQueryResponse.Result.Any())
        {
            if (playlistQueryResponse.Failure != null)
            {
                logger.LogPlaylistFetchFailure(podcast, playlistQueryResponse.Failure, youTubePlaylistId);
                return new GetPlaylistEpisodesResponse(null, isExpensiveQuery, playlistQueryResponse.Failure);
            }

            // Nothing in the window: no episodes to build, so the walk's outcome stands (a complete walk of a large
            // playlist with nothing new still refreshes the YouTube indexing hints).
            return new GetPlaylistEpisodesResponse(null, isExpensiveQuery,
                TotalResults: playlistQueryResponse.TotalResults,
                WalkOutcome: playlistQueryResponse.Result == null
                    ? YouTubePlaylistWalkOutcome.Incomplete
                    : playlistQueryResponse.WalkOutcome);
        }

        var built = await BuildPlaylistEpisodes(podcast, youTubeChannelId, indexingContext,
            playlistQueryResponse.Result, knownIds);
        return built with
        {
            IsExpensiveQuery = isExpensiveQuery,
            TotalResults = playlistQueryResponse.TotalResults,
            // Hints may only move past items that were actually built.
            WalkOutcome = built.Results != null
                ? playlistQueryResponse.WalkOutcome
                : YouTubePlaylistWalkOutcome.Incomplete
        };
    }

    public async Task<GetPlaylistEpisodesResponse> BuildPlaylistEpisodes(
        Podcast podcast,
        YouTubeChannelId? youTubeChannelId,
        IndexingContext indexingContext,
        IList<PlaylistItem> playlistItems,
        IEnumerable<string>? knownIds = null)
    {
        IList<PlaylistItem> results = playlistItems;
        if (indexingContext.ReleasedSince.HasValue)
        {
            results = results.Where(x =>
                x.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
        }

        // Same as the channel path: a video already stored for this podcast is not re-fetched (videos.list),
        // re-probed (thumbnails) or re-built; discovery only needs the new ones.
        var knownSkipped = 0;
        if (knownIds != null)
        {
            var known = knownIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
            if (known.Count > 0)
            {
                var inWindow = results.Count;
                results = results.Where(x => !known.Contains(x.Snippet.ResourceId.VideoId)).ToList();
                knownSkipped = inWindow - results.Count;
            }
        }

        if (!results.Any())
        {
            return new GetPlaylistEpisodesResponse([], KnownSkipped: knownSkipped);
        }

        var videoDetails =
            await youTubeVideoService.GetVideoContentDetails(
                youTubeService,
                results.Select(x => x.Snippet.ResourceId.VideoId),
                indexingContext,
                withSnippets: true,
                withStatistics: true);
        if (videoDetails != null && videoDetails.Any())
        {
            try
            {
                var reducedResults = new List<EpisodeModel>();
                var candidates = results
                    .Where(x => x.Snippet != null)
                    .Where(x => x.Snippet.Title != "Deleted video")
                    .Select(x =>
                        new PlaylistItemVideo(
                            x,
                            videoDetails.SingleOrDefault(videoDetail =>
                                videoDetail.Id == x.Snippet.ResourceId.VideoId)!
                        ))
                    .Where(x => x.VideoDetails?.Snippet != null)
                    .ToList();
                var otherChannel = youTubeChannelId == null
                    ? []
                    : candidates
                        .Where(x => x.VideoDetails.Snippet.ChannelId != youTubeChannelId.ChannelId)
                        .ToList();
                if (otherChannel.Count > 0)
                {
                    LogOtherChannelVideosDropped(podcast, youTubeChannelId!, candidates.Count, otherChannel);
                }

                foreach (var playlistItemVideo in candidates.Except(otherChannel))
                {
                    if (SkipMembersOnly(playlistItemVideo.VideoDetails))
                    {
                        continue;
                    }

                    reducedResults.Add(await BuildEpisodeAsync(
                        playlistItemVideo.PlaylistItem.Snippet,
                        playlistItemVideo.VideoDetails,
                        indexingContext));
                }

                return new GetPlaylistEpisodesResponse(reducedResults, KnownSkipped: knownSkipped);
            }
            catch (Exception e) when (e is not OperationCanceledException ||
                                      !indexingContext.CancellationToken.IsCancellationRequested)
            {
                logger.LogError(e,
                    "Error getting playlist videos. youtube-channel-id: '{youtubeChannelId}'.",
                    youTubeChannelId?.ChannelId);
            }
        }

        return new GetPlaylistEpisodesResponse(null);
    }

    /// <summary>
    /// A playlist video uploaded by a channel other than the parent's <c>youTubeChannelId</c> is not indexed. That is
    /// normally a sign the parent's playlist belongs to a different channel (e.g. Fame Under Fire: BBC World Service
    /// channel id with a BBC News Entertainment playlist, where every video was dropped). Log it so it is never silent.
    /// </summary>
    private void LogOtherChannelVideosDropped(
        Podcast podcast,
        YouTubeChannelId youTubeChannelId,
        int candidateCount,
        IReadOnlyCollection<PlaylistItemVideo> dropped)
    {
        const int maxListed = 20;
        var droppedChannels = dropped
            .GroupBy(x => x.VideoDetails.Snippet.ChannelId)
            .Select(g => $"{g.Key}={g.Count()}")
            .ToArray();
        logger.LogWarning(
            "YouTubePlaylistOtherChannelVideosDropped podcast-id='{PodcastId}' podcast-name='{PodcastName}' playlist-id='{PlaylistId}' youtube-channel-id='{YouTubeChannelId}' dropped-count='{DroppedCount}' candidate-count='{CandidateCount}' all-dropped='{AllDropped}' video-channels='{VideoChannels}' video-ids='{VideoIds}'. Playlist videos uploaded by another channel are not indexed; check the playlist belongs to the YouTube channel.",
            podcast.Id,
            podcast.Name,
            podcast.YouTubePlaylistId,
            youTubeChannelId.ChannelId,
            dropped.Count,
            candidateCount,
            dropped.Count == candidateCount,
            string.Join(",", droppedChannels),
            string.Join(",", dropped.Take(maxListed).Select(x => x.VideoDetails.Id)) +
            (dropped.Count > maxListed ? $",...(+{dropped.Count - maxListed})" : string.Empty));
    }

    private bool SkipMembersOnly(Google.Apis.YouTube.v3.Data.Video video)
    {
        if (!video.IsMembersOnly())
        {
            return false;
        }

        logger.LogWarning(
            "Skipping YouTube video '{VideoId}' ('{VideoTitle}') because it is members-only (statistics.viewCount is absent while other statistics are present).",
            video.Id,
            video.Snippet?.Title);
        return true;
    }

    private record PlaylistItemVideo(PlaylistItem PlaylistItem, Google.Apis.YouTube.v3.Data.Video VideoDetails);
}
