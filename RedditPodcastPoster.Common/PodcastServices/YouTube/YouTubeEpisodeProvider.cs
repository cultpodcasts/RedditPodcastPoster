using System.Xml;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeEpisodeProvider : IYouTubeEpisodeProvider
{
    private readonly ILogger<YouTubeEpisodeProvider> _logger;
    private readonly IYouTubeSearchService _youTubeSearchService;

    public YouTubeEpisodeProvider(
        IYouTubeSearchService youTubeSearchService,
        ILogger<YouTubeEpisodeProvider> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _logger = logger;
    }

    public async Task<IList<Episode>?> GetEpisodes(
        YouTubeChannelId request, 
        IndexingContext indexingContext,
        IEnumerable<string> knownIds)
    {
        var youTubeVideos =
            await _youTubeSearchService.GetLatestChannelVideoSnippets(
                new YouTubeChannelId(request.ChannelId), indexingContext);
        if (youTubeVideos != null)
        {
            var youTubeVideoIds = youTubeVideos.Select(x => x.Id.VideoId);
            youTubeVideoIds = youTubeVideoIds.Where(x => !knownIds.Contains(x));

            if (youTubeVideoIds.Any())
            {
                var videoDetails =
                    await _youTubeSearchService.GetVideoContentDetails(youTubeVideoIds, indexingContext);

                if (videoDetails != null)
                {
                    return youTubeVideos.Select(searchResult => GetEpisode(
                            searchResult,
                            videoDetails.SingleOrDefault(videoDetail => videoDetail.Id == searchResult.Id.VideoId)!))
                        .ToList();
                }
            }
        }

        return null;
    }

    public Episode GetEpisode(SearchResult searchResult, Video videoDetails)
    {
        return Episode.FromYouTube(
            searchResult.Id.VideoId,
            searchResult.Snippet.Title,
            searchResult.Snippet.Description,
            XmlConvert.ToTimeSpan(videoDetails.ContentDetails.Duration),
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            searchResult.ToYouTubeUrl());
    }

    public Episode GetEpisode(PlaylistItemSnippet playlistItemSnippet, Video videoDetails)
    {
        return Episode.FromYouTube(
            playlistItemSnippet.ResourceId.VideoId,
            playlistItemSnippet.Title,
            playlistItemSnippet.Description,
            XmlConvert.ToTimeSpan(videoDetails.ContentDetails.Duration),
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            playlistItemSnippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            playlistItemSnippet.ToYouTubeUrl());
    }

    public async Task<IList<Episode>?> GetPlaylistEpisodes(
        YouTubePlaylistId youTubePlaylistId, IndexingContext indexingContext)
    {
        var playlistVideos = await _youTubeSearchService.GetPlaylistVideoSnippets(new YouTubePlaylistId(
            youTubePlaylistId.PlaylistId), indexingContext);
        if (playlistVideos != null && playlistVideos.Any())
        {
            if (indexingContext.ReleasedSince.HasValue)
            {
                playlistVideos = playlistVideos.Where(x =>
                    x.Snippet.PublishedAtDateTimeOffset.ReleasedSinceDate(indexingContext.ReleasedSince)).ToList();
            }
            var videoDetails =
                await _youTubeSearchService.GetVideoContentDetails(playlistVideos.Select(x => x.Snippet.ResourceId.VideoId),
                    indexingContext);
            if (videoDetails != null)
            {
                return playlistVideos.Select(playlistItem => GetEpisode(
                        playlistItem.Snippet,
                        videoDetails.SingleOrDefault(videoDetail =>
                            videoDetail.Id == playlistItem.Snippet.ResourceId.VideoId)!))
                    .ToList();
            }
        }

        return null;
    }
}