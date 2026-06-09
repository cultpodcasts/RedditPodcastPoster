using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Episode;

public class YouTubeEpisodeProvider(
    IYouTubeServiceWrapper youTubeService,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeVideoService youTubeVideoService,
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
    IYouTubeChannelVideoRetrievalPolicy youTubeChannelVideoRetrievalPolicy,
    ILogger<YouTubeEpisodeProvider> logger)
    : IYouTubeEpisodeProvider
{
    public async Task<IList<RedditPodcastPoster.Models.Episode>?> GetEpisodes(
        RedditPodcastPoster.Models.Podcast podcast,
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
                            true);

                    if (videoDetails != null)
                    {
                        return videoDetails
                            .Where(videoDetail => YouTubeVideoDurationMatcher.HasDuration(videoDetail.GetLength()))
                            .Select(videoDetail =>
                                GetEpisode(
                                    youTubeVideos.First(searchResult => searchResult.Id.VideoId == videoDetail.Id),
                                    videoDetail))
                            .ToList();
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

    private async Task<IList<RedditPodcastPoster.Models.Episode>?> GetEpisodesFromChannelUploadsPlaylist(
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
            true);
        if (videoDetails == null || !videoDetails.Any())
        {
            return null;
        }

        return playlistItems
            .Where(x => x.Snippet.Title != "Deleted video")
            .Select(x =>
                new PlaylistItemVideo(
                    x,
                    videoDetails.SingleOrDefault(videoDetail => videoDetail.Id == x.Snippet.ResourceId.VideoId)!))
            .Where(x => x.VideoDetails?.Snippet != null)
            .Where(x => YouTubeVideoDurationMatcher.HasDuration(x.VideoDetails.GetLength()))
            .Where(x => x.VideoDetails.Snippet.ChannelId == request.ChannelId)
            .Select(x => GetEpisode(x.PlaylistItem.Snippet, x.VideoDetails))
            .ToList();
    }

    public RedditPodcastPoster.Models.Episode GetEpisode(SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails)
    {
        return RedditPodcastPoster.Models.Episode.FromYouTube(
            searchResult.Id.VideoId,
            searchResult.Snippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength() ?? TimeSpan.Zero,
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            searchResult.ToYouTubeUrl(),
            videoDetails.GetImageUrl());
    }

    public RedditPodcastPoster.Models.Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails)
    {
        return RedditPodcastPoster.Models.Episode.FromYouTube(
            playlistItemSnippet.ResourceId.VideoId,
            playlistItemSnippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength() ?? TimeSpan.Zero,
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            playlistItemSnippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            playlistItemSnippet.ToYouTubeUrl(),
            videoDetails.GetImageUrl());
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
                true);
        if (videoDetails != null && videoDetails.Any())
        {
            try
            {
                var reducedResults = results
                    .Where(x => x.Snippet != null)
                    .Where(x => x.Snippet.Title != "Deleted video")
                    .Select(x =>
                        new PlaylistItemVideo(
                            x,
                            videoDetails.SingleOrDefault(videoDetail =>
                                videoDetail.Id == x.Snippet.ResourceId.VideoId)!
                        )
                    )
                    .Where(x => x.VideoDetails?.Snippet != null)
                    .Where(x => youTubeChannelId == null ||
                                x.VideoDetails.Snippet.ChannelId == youTubeChannelId.ChannelId)
                    .Select(x => GetEpisode(x.PlaylistItem.Snippet, x.VideoDetails))
                    .ToList();
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

    private record PlaylistItemVideo(PlaylistItem PlaylistItem, Google.Apis.YouTube.v3.Data.Video VideoDetails);
}