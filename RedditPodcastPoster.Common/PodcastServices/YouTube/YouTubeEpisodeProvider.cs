using System.Xml;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeEpisodeProvider : IYouTubeEpisodeProvider
{
    private readonly ILogger<YouTubeEpisodeProvider> _logger;
    private readonly IYouTubeItemResolver _youTubeItemResolver;
    private readonly IYouTubeSearchService _youTubeSearchService;

    public YouTubeEpisodeProvider(
        IYouTubeSearchService youTubeSearchService,
        IYouTubeItemResolver youTubeItemResolver,
        ILogger<YouTubeEpisodeProvider> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _youTubeItemResolver = youTubeItemResolver;
        _logger = logger;
    }

    public async Task<IList<Episode>?> GetEpisodes(YouTubeChannelId request, IndexingContext indexingContext)
    {
        var youTubeVideos =
            await _youTubeSearchService.GetLatestChannelVideos(
                new YouTubeChannelId(request.ChannelId), indexingContext);
        if (youTubeVideos != null)
        {
            var videoDetails =
                await _youTubeSearchService.GetVideoDetails(youTubeVideos.Select(x => x.Id.VideoId), indexingContext);

            if (videoDetails != null)
            {
                return youTubeVideos.Select(searchResult => GetEpisode(
                        searchResult,
                        videoDetails.SingleOrDefault(videoDetail => videoDetail.Id == searchResult.Id.VideoId)!))
                    .ToList();
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
        var playlistVideos = await _youTubeSearchService.GetPlaylist(new YouTubePlaylistId(
            youTubePlaylistId.PlaylistId), indexingContext);
        if (playlistVideos != null)
        {
            var videoDetails =
                await _youTubeSearchService.GetVideoDetails(playlistVideos.Select(x => x.Snippet.ResourceId.VideoId),
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