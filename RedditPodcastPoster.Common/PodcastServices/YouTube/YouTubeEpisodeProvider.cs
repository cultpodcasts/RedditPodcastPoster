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

    public async Task<IList<Episode>?> GetEpisodes(Podcast podcast, DateTime? processRequestReleasedSince)
    {
        var youTubeVideos = await _youTubeSearchService.GetLatestChannelVideos(podcast, processRequestReleasedSince);
        var videoDetails =
            await _youTubeSearchService.GetVideoDetails(youTubeVideos.Select(x => x.Id.VideoId));

        return youTubeVideos.Select(searchResult => GetEpisode(
                searchResult, videoDetails.SingleOrDefault(videoDetail => videoDetail.Id == searchResult.Id.VideoId)!))
            .ToList();
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
}