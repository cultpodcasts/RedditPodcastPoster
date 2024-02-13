using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeEpisodeProvider(
    IYouTubePlaylistService youTubePlaylistService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<YouTubeEpisodeProvider> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IYouTubeEpisodeProvider
{
    public async Task<IList<Episode>?> GetEpisodes(
        YouTubeChannelId request,
        IndexingContext indexingContext,
        IEnumerable<string> knownIds)
    {
        var youTubeVideos =
            await youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(
                new YouTubeChannelId(request.ChannelId), indexingContext);
        if (youTubeVideos != null)
        {
            var youTubeVideoIds = youTubeVideos.Select(x => x.Id.VideoId);
            youTubeVideoIds = youTubeVideoIds.Where(x => !knownIds.Contains(x));

            if (youTubeVideoIds.Any())
            {
                var videoDetails =
                    await youTubeVideoService.GetVideoContentDetails(youTubeVideoIds, indexingContext, true);

                if (videoDetails != null)
                {
                    return videoDetails.Select(videoDetail =>
                        GetEpisode(youTubeVideos.First(searchResult => searchResult.Id.VideoId == videoDetail.Id),
                            videoDetail)).ToList();
                }
            }
        }

        return null;
    }

    public Episode GetEpisode(SearchResult searchResult, Video videoDetails)
    {
        return Episode.FromYouTube(
            searchResult.Id.VideoId,
            searchResult.Snippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength(),
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            searchResult.ToYouTubeUrl());
    }

    public Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet, Video videoDetails)
    {
        return Episode.FromYouTube(
            playlistItemSnippet.ResourceId.VideoId,
            playlistItemSnippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength(),
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            playlistItemSnippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            playlistItemSnippet.ToYouTubeUrl());
    }

    public async Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
        YouTubePlaylistId youTubePlaylistId, IndexingContext indexingContext)
    {
        var playlistQueryResponse = await youTubePlaylistService.GetPlaylistVideoSnippets(new YouTubePlaylistId(
            youTubePlaylistId.PlaylistId), indexingContext);
        var isExpensiveQuery = playlistQueryResponse.IsExpensiveQuery;
        if (playlistQueryResponse.Result != null && playlistQueryResponse.Result.Any())
        {
            var results = playlistQueryResponse.Result;
            if (indexingContext.ReleasedSince.HasValue)
            {
                results = results.Where(x =>
                    x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
            }

            var videoDetails =
                await youTubeVideoService.GetVideoContentDetails(results.Select(x => x.Snippet.ResourceId.VideoId),
                    indexingContext, true);
            if (videoDetails != null)
            {
                return new GetPlaylistEpisodesResponse(results.Where(x=>x.Snippet.Title!= "Deleted video").Select(playlistItem => GetEpisode(
                        playlistItem.Snippet,
                        videoDetails.SingleOrDefault(videoDetail =>
                            videoDetail.Id == playlistItem.Snippet.ResourceId.VideoId)!))
                    .ToList(), isExpensiveQuery);
            }
        }

        return new GetPlaylistEpisodesResponse(null, isExpensiveQuery);
    }
}