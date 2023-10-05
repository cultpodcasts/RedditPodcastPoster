using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public class PodcastServicesEpisodeEnricher : IPodcastServicesEpisodeEnricher
{
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly ILogger<PodcastServicesEpisodeEnricher> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;
    private readonly IYouTubeItemResolver _youTubeItemResolver;
    private readonly IApplePodcastEnricher _applePodcastEnricher;

    public PodcastServicesEpisodeEnricher(
        ISpotifyItemResolver spotifyItemResolver,
        IAppleEpisodeResolver appleEpisodeResolver,
        IYouTubeItemResolver youTubeItemResolver,
        IApplePodcastEnricher applePodcastEnricher,
        ILogger<PodcastServicesEpisodeEnricher> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _appleEpisodeResolver = appleEpisodeResolver;
        _youTubeItemResolver = youTubeItemResolver;
        _applePodcastEnricher = applePodcastEnricher;
        _logger = logger;
    }

    public async Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext
    )
    {
        var results= new EnrichmentResults();
        foreach (var episode in newEpisodes)
        {
            var enrichmentContext= new EnrichmentContext();
            var enrichmentRequest = new EnrichmentRequest(podcast, episode);
            foreach (Service service in Enum.GetValues(typeof(Service)))
            {
                switch (service)
                {
                    case Service.Spotify
                        when episode.Urls.Spotify == null || string.IsNullOrWhiteSpace(episode.SpotifyId):
                        await EnrichFromSpotify(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.Apple when episode.Urls.Apple == null || episode.AppleId == 0:
                        await EnrichFromApple(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                    case Service.YouTube when !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) && (episode.Urls.YouTube == null || string.IsNullOrWhiteSpace(episode.YouTubeId)):
                        await EnrichFromYouTube(enrichmentRequest, indexingContext, enrichmentContext);
                        break;
                }
            }
            if (enrichmentContext.Updated)
            {
                _logger.LogInformation($"Enriched episode '{episode.Title}'. YouTube:'{enrichmentContext.YouTube}', Apple:'{enrichmentContext.Apple}', Spotify:'{enrichmentContext.Spotify}'.");
                results.UpdatedEpisodes.Add(enrichmentContext.ToEnrichmentResult());
            }
        }
        return results;
    }

    private async Task EnrichFromYouTube(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            _logger.LogInformation($"{nameof(EnrichFromYouTube)} Bypassing enriching of '{request.Episode.Title}' with release-date of '{request.Episode.Release:R}' from YouTube as is below the {nameof(request.Podcast.YouTubePublishingDelayTimeSpan)} which is '{request.Podcast.YouTubePublishingDelayTimeSpan}'.");
            return;
        }

        var youTubeItem = await _youTubeItemResolver.FindEpisode(request, indexingContext);
        if (!string.IsNullOrWhiteSpace(youTubeItem?.Id.VideoId))
        {
            _logger.LogInformation($"{nameof(EnrichFromApple)} Found matching YouTube episode: '{youTubeItem.Id.VideoId}' with title '{youTubeItem.Snippet.Title}' and release-date '{youTubeItem.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime:R}'.");
            request.Episode.YouTubeId = youTubeItem.Id.VideoId;
            var url = youTubeItem.ToYouTubeUrl();
            request.Episode.Urls.YouTube = url;
            enrichmentContext.Updated = true;
            enrichmentContext.YouTube = url;
        }
    }

    private async Task EnrichFromApple(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.AppleId == null)
        {
            await _applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId != null)
        {
            var appleItem =
                await _appleEpisodeResolver.FindEpisode(FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode), indexingContext);
            if (appleItem != null)
            {
                _logger.LogInformation($"{nameof(EnrichFromApple)} Found matching Apple episode: '{appleItem.Id}' with title '{appleItem.Title}' and release-date '{appleItem.Release:R}'.");
                var url = appleItem.Url.CleanAppleUrl();
                request.Episode.Urls.Apple = url;
                request.Episode.AppleId = appleItem.Id;
                if (request.Episode.Release.TimeOfDay == TimeSpan.Zero)
                {
                    request.Episode.Release = appleItem.Release;
                }
                enrichmentContext.Apple = url;
                enrichmentContext.Updated = true;
            }
        }
    }

    private async Task EnrichFromSpotify(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
    {
        var spotifyEpisode = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(request.Podcast, request.Episode), indexingContext);
        if (spotifyEpisode != null)
        {
            _logger.LogInformation($"{nameof(EnrichFromSpotify)} Found matching Spotify episode: '{spotifyEpisode.Id}' with title '{spotifyEpisode.Name}' and release-date '{spotifyEpisode.ReleaseDate}'.");
            request.Episode.SpotifyId = spotifyEpisode.Id;
            var url = spotifyEpisode.GetUrl();
            request.Episode.Urls.Spotify = url;
            enrichmentContext.Updated = true;
            enrichmentContext.Spotify = url;
        }
    }
}