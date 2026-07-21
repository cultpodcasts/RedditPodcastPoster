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
    IYouTubeVideoService youTubeVideoService,
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
            return await GetEpisodesFromChannelUploadsPlaylist(channelId, indexingContext, knownIds);
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
            return await GetEpisodesFromChannelUploadsPlaylist(channelId, indexingContext, knownIds);
        }
    }

    private async Task<IList<EpisodeModel>?> GetEpisodesFromChannelUploadsPlaylist(
        YouTubeChannelId request,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds)
    {
        var channelVideos = await youTubeChannelVideosService.GetChannelVideos(request, indexingContext);
        if (channelVideos?.PlaylistItems == null)
        {
            return null;
        }

        var playlistItems = channelVideos.PlaylistItems
            .Where(x => !knownIds.Contains(x.Snippet.ResourceId.VideoId))
            .ToList();
        if (indexingContext.ReleasedSince.HasValue)
        {
            playlistItems = playlistItems
                .Where(x => x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince))
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

            episodes.Add(await GetEpisodeAsync(playlistItemVideo.PlaylistItem.Snippet, playlistItemVideo.VideoDetails));
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

    public async Task<EpisodeModel> GetEpisodeAsync(PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails)
    {
        var image = await youTubeThumbnailResolver.GetImageUrlAsync(videoDetails);
        var candidate = youTubeEpisodeAdapter.Adapt(
            playlistItemSnippet.ToCatalogueInput(videoDetails, image));
        var isExplicit = videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted";
        return episodeFromCandidateFactory.Create(candidate, isExplicit);
    }

    public async Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(YouTubePlaylistId youTubePlaylistId,
        YouTubeChannelId? youTubeChannelId, IndexingContext indexingContext, bool expensivePlaylist = false)
    {
        var playlistQueryResponse = await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(
            youTubePlaylistId.PlaylistId), indexingContext, expensivePlaylist);
        var isExpensiveQuery = playlistQueryResponse.IsExpensiveQuery;
        if (playlistQueryResponse.Result == null || !playlistQueryResponse.Result.Any())
        {
            return new GetPlaylistEpisodesResponse(null, isExpensiveQuery);
        }

        var results = playlistQueryResponse.Result;
        if (indexingContext.ReleasedSince.HasValue)
        {
            results = results.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
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
                foreach (var playlistItemVideo in results
                             .Where(x => x.Snippet != null)
                             .Where(x => x.Snippet.Title != "Deleted video")
                             .Select(x =>
                                 new PlaylistItemVideo(
                                     x,
                                     videoDetails.SingleOrDefault(videoDetail =>
                                         videoDetail.Id == x.Snippet.ResourceId.VideoId)!
                                 ))
                             .Where(x => x.VideoDetails?.Snippet != null)
                             .Where(x => youTubeChannelId == null ||
                                         x.VideoDetails.Snippet.ChannelId == youTubeChannelId.ChannelId))
                {
                    if (SkipMembersOnly(playlistItemVideo.VideoDetails))
                    {
                        continue;
                    }

                    reducedResults.Add(await GetEpisodeAsync(
                        playlistItemVideo.PlaylistItem.Snippet,
                        playlistItemVideo.VideoDetails));
                }

                return new GetPlaylistEpisodesResponse(reducedResults, isExpensiveQuery);
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Error getting playlist videos. youtube-channel-id: '{youtubeChannelId}', youtube-channel-id-value: '{youtubeChannelIdValue}'",
                    youTubeChannelId,
                    youTubeChannelId?.ChannelId);
            }
        }

        return new GetPlaylistEpisodesResponse(null, isExpensiveQuery);
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
