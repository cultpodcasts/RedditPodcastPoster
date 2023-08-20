using System.Xml;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeEpisodeProvider : IYouTubeEpisodeProvider
{
    private readonly ILogger<YouTubeEpisodeProvider> _logger;
    private readonly IYouTubeSearchService _youTubeSearchService;
    private readonly IYouTubeUrlResolver _youTubeUrlResolver;

    public YouTubeEpisodeProvider(
        IYouTubeSearchService youTubeSearchService,
        IYouTubeUrlResolver youTubeUrlResolver,
        ILogger<YouTubeEpisodeProvider> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _youTubeUrlResolver = youTubeUrlResolver;
        _logger = logger;
    }

    public async Task<IList<Episode>?> GetEpisodes(Podcast podcast, DateTime? processRequestReleasedSince)
    {
        var episodes = new List<Episode>();
        var batch = _youTubeSearchService.GetLatestChannelVideos(podcast, processRequestReleasedSince);
        SearchListResponse batchResults;
        do
        {
            batchResults = await batch;
            var videoDetails =
                await _youTubeSearchService.GetVideoDetails(batchResults.Items.Select(x => x.Id.VideoId));
            episodes.AddRange(
                batchResults.Items.Select(
                    x => GetEpisode(
                        x, videoDetails.Items.SingleOrDefault(y => y.Id == x.Id.VideoId))));
            batch = _youTubeSearchService.GetLatestChannelVideos(podcast, processRequestReleasedSince,
                pageToken: batchResults.NextPageToken);
        } while (!string.IsNullOrWhiteSpace(batchResults.NextPageToken) &&
                 Active(processRequestReleasedSince, episodes.Last().Release));

        return episodes;
    }

    private static bool Active(DateTime? processRequestReleasedSince, DateTime release)
    {
        if (!processRequestReleasedSince.HasValue)
        {
            return true;
        }

        return release > processRequestReleasedSince.Value;
    }

    private Episode GetEpisode(SearchResult x, Video videoDetails)
    {
        return Episode.FromYouTube(
            x.Id.VideoId,
            x.Snippet.Title,
            x.Snippet.Description,
            XmlConvert.ToTimeSpan(videoDetails.ContentDetails.Duration),
            videoDetails.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
            x.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime,
            _youTubeUrlResolver.GetYouTubeUrl(x));
    }
}