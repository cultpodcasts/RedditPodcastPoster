using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeEpisodeEnricher : IYouTubeEpisodeEnricher
{
    private readonly ILogger<YouTubeEpisodeEnricher> _logger;
    private readonly IYouTubeIdExtractor _youTubeIdExtractor;
    private readonly IYouTubeItemResolver _youTubeItemResolver;

    public YouTubeEpisodeEnricher(
        IYouTubeItemResolver youTubeItemResolver,
        IYouTubeIdExtractor youTubeIdExtractor,
        ILogger<YouTubeEpisodeEnricher> logger)
    {
        _youTubeItemResolver = youTubeItemResolver;
        _youTubeIdExtractor = youTubeIdExtractor;
        _logger = logger;
    }

    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            _logger.LogInformation(
                $"{nameof(Enrich)} Bypassing enriching of '{request.Episode.Title}' with release-date of '{request.Episode.Release:R}' from YouTube as is below the {nameof(request.Podcast.YouTubePublishingDelayTimeSpan)} which is '{request.Podcast.YouTubePublishingDelayTimeSpan}'.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Episode.YouTubeId) && request.Episode.Urls.YouTube == null)
        {
            var url = SearchResultExtensions.ToYouTubeUrl(request.Episode.YouTubeId);
            request.Episode.Urls.YouTube = url;
            enrichmentContext.YouTube = url;
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Episode.YouTubeId) && request.Episode.Urls.YouTube != null)
        {
            var videoId = _youTubeIdExtractor.Extract(request.Episode.Urls.YouTube);
            if (videoId != null)
            {
                request.Episode.YouTubeId = videoId;
                enrichmentContext.YouTubeId = videoId;
                return;
            }
        }

        var youTubeItem = await _youTubeItemResolver.FindEpisode(request, indexingContext);
        if (!string.IsNullOrWhiteSpace(youTubeItem?.SearchResult?.Id.VideoId))
        {
            var episodeYouTubeId = youTubeItem.SearchResult.Id.VideoId;
            var release = youTubeItem.SearchResult.Snippet.PublishedAtDateTimeOffset;

            _logger.LogInformation(
                $"{nameof(Enrich)} Found matching YouTube episode: '{episodeYouTubeId}' with title '{youTubeItem.SearchResult.Snippet.Title}' and release-date '{release!.Value.UtcDateTime:R}''.");

            request.Episode.YouTubeId = enrichmentContext.YouTubeId = episodeYouTubeId;
            request.Episode.Urls.YouTube = enrichmentContext.YouTube = youTubeItem.SearchResult.ToYouTubeUrl();

            if (request.Podcast.AppleId == null &&
                request.Episode.Release.TimeOfDay == TimeSpan.Zero &&
                release.HasValue)
            {
                request.Episode.Release = enrichmentContext.Release = release!.Value.UtcDateTime;
            }
        }
    }
}