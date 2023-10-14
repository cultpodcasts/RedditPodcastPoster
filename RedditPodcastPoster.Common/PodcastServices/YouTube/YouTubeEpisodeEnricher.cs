using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.UrlCategorisation;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeEpisodeEnricher : IYouTubeEpisodeEnricher
{
    private readonly IYouTubeItemResolver _youTubeItemResolver;
    private readonly IYouTubeIdExtractor _youTubeIdExtractor;
    private readonly ILogger<YouTubeEpisodeEnricher> _logger;

    public YouTubeEpisodeEnricher(
        IYouTubeItemResolver youTubeItemResolver,
        IYouTubeIdExtractor youTubeIdExtractor,
        ILogger<YouTubeEpisodeEnricher> logger)
    {
        _youTubeItemResolver = youTubeItemResolver;
        _youTubeIdExtractor = youTubeIdExtractor;
        _logger = logger;
    }

    public async Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
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
        if (!string.IsNullOrWhiteSpace(youTubeItem?.Id.VideoId))
        {
            var episodeYouTubeId = youTubeItem.Id.VideoId;
            _logger.LogInformation(
                $"{nameof(Enrich)} Found matching YouTube episode: '{episodeYouTubeId}' with title '{youTubeItem.Snippet.Title}' and release-date '{youTubeItem.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime:R}'.");
            request.Episode.YouTubeId = episodeYouTubeId;
            enrichmentContext.YouTubeId = episodeYouTubeId;
            var url = youTubeItem.ToYouTubeUrl();
            request.Episode.Urls.YouTube = url;
            enrichmentContext.YouTube = url;
        }
    }
}