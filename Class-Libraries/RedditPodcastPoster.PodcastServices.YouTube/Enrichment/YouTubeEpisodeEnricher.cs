using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

public class YouTubeEpisodeEnricher(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeItemResolver youTubeItemResolver,
    ITextSanitiser textSanitiser,
    IYouTubeVideoService youTubeVideoService,
    ILogger<YouTubeEpisodeEnricher> logger)
    : IYouTubeEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            var timeSpan = request.Podcast.YouTubePublishingDelay().ToString("g");
            logger.LogInformation(
                $"{nameof(Enrich)} Bypassing enriching of '{request.Episode.Title}' with release-date of '{request.Episode.Release:R}' from YouTube as is below the {nameof(request.Podcast.YouTubePublishingDelay)} which is '{timeSpan}'.");
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
            var videoId = YouTubeIdResolver.Extract(request.Episode.Urls.YouTube);
            if (videoId != null)
            {
                request.Episode.YouTubeId = videoId;
                enrichmentContext.YouTubeId = videoId;
                return;
            }
        }

        var youTubeItem = await youTubeItemResolver.FindEpisode(request, indexingContext);
        if (!string.IsNullOrWhiteSpace(youTubeItem?.SearchResult?.Id.VideoId) &&
            request.Podcast.Episodes.All(x => x.YouTubeId != youTubeItem.SearchResult.Id.VideoId))
        {
            var episodeYouTubeId = youTubeItem.SearchResult.Id.VideoId;
            var release = youTubeItem.SearchResult.Snippet.PublishedAtDateTimeOffset;
            if (release != null)
            {
                logger.LogInformation(
                    $"{nameof(Enrich)} Found matching YouTube episode: '{episodeYouTubeId}' with title '{youTubeItem.SearchResult.Snippet.Title}' and release-date '{release.Value.UtcDateTime:R}'.");
            }
            else
            {
                logger.LogInformation(
                    $"{nameof(Enrich)} Found matching YouTube episode: '{episodeYouTubeId}' with title '{youTubeItem.SearchResult.Snippet.Title}'.");
            }

            request.Episode.YouTubeId = enrichmentContext.YouTubeId = episodeYouTubeId;
            request.Episode.Urls.YouTube = enrichmentContext.YouTube = youTubeItem.SearchResult.ToYouTubeUrl();

            if (string.IsNullOrWhiteSpace(request.Episode.Description) || request.Episode.Images?.YouTube == null)
            {
                var videoContentDetails =
                    await youTubeVideoService.GetVideoContentDetails(youTubeService, [episodeYouTubeId],
                        indexingContext, true);
                var item = videoContentDetails?.FirstOrDefault();
                if (item != null)
                {
                    var description = item.Snippet.Description.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        if (!string.IsNullOrWhiteSpace(request.Podcast.DescriptionRegex))
                        {
                            request.Episode.Description = textSanitiser.SanitiseDescription(
                                description, new Regex(request.Podcast.DescriptionRegex));
                        }
                        else
                        {
                            request.Episode.Description = description;
                        }
                    }

                    var image = item.GetImageUrl();
                    if (image != null)
                    {
                        request.Episode.Images ??= new EpisodeImages();
                        request.Episode.Images.YouTube = image;
                    }
                }
            }

            if ((request.Podcast.AppleId == null || request.Episode.AppleId == null) &&
                request.Episode.Release.TimeOfDay == TimeSpan.Zero &&
                release.HasValue)
            {
                request.Episode.Release = enrichmentContext.Release = release!.Value.UtcDateTime;
                enrichmentContext.Release = release!.Value.UtcDateTime;
            }
        }
    }
}