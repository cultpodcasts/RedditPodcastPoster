using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

namespace RedditPodcastPoster.PodcastServices.Enrichers;

public class PodcastServicesEpisodeEnricher(
    IAppleEpisodeEnricher appleEpisodeEnricher,
    ISpotifyEpisodeEnricher spotifyEpisodeEnricher,
    IYouTubeEpisodeEnricher youTubeEpisodeEnricher,
    IPodcastEpisodeFilter podcastEpisodeFilter,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastServicesEpisodeEnricher> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastServicesEpisodeEnricher
{
    public async Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext
    )
    {
        var results = new List<EnrichmentResult>();
        var cancellationToken = PodcastRunScope.Current.CancellationToken;
        foreach (var episode in newEpisodes)
        {
            // The indexer's per-podcast budget: stop between calls so a slow enrichment is cancelled, not abandoned.
            cancellationToken.ThrowIfCancellationRequested();
            var enrichmentContext = new EnrichmentContext();
            var enrichmentRequest = new EnrichmentRequest(podcast, episodes, episode);

            // In the Index flow the planner plans nothing for a podcast with no Spotify show id (stage 'skipped-no-show'),
            // so the call does no work and only writes a miss Warning. Other flows may search by show name.
            var skipSpotifyNoShow = indexingContext.LookupFlow == EpisodeLookupFlow.Index &&
                                    string.IsNullOrWhiteSpace(podcast.SpotifyId);
            if (!skipSpotifyNoShow &&
                (EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify) == null ||
                 string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode))))
            {
                await spotifyEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
            }

            if (EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple) == null ||
                EpisodeServicePresence.AppleEpisodeId(episode) is null or 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await appleEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
            }

            if (podcast.SkipEnrichingFromYouTube is null or false &&
                podcast.HasYouTubeSource() &&
                (EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube) == null ||
                 string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
            }

            if (enrichmentContext.Updated)
            {
                results.Add(new EnrichmentResult(podcast, episode, enrichmentContext));
            }
        }

        if (podcast.SkipEnrichingFromYouTube == null &&
            podcast.HasYouTubeSource())
        {
            if (podcast.YouTubePublishingDelay() > TimeSpan.Zero)
            {
                var delayedEpisodes = episodes
                    .Where(episode => podcastEpisodeFilter.IsRecentlyExpiredDelayedPublishing(podcast, episode))
                    .Where(delayedEpisode => !newEpisodes.Contains(delayedEpisode))
                    .Where(delayedEpisode =>
                        EpisodeServicePresence.TryGetUrl(delayedEpisode, ServiceKeys.YouTube) == null ||
                        string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(delayedEpisode)));
                foreach (var delayedEpisode in delayedEpisodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var enrichmentContext = new EnrichmentContext();
                    var enrichmentRequest = new EnrichmentRequest(podcast, episodes, delayedEpisode);
                    await youTubeEpisodeEnricher.Enrich(enrichmentRequest, indexingContext, enrichmentContext);
                    if (enrichmentContext.Updated)
                    {
                        results.Add(new EnrichmentResult(podcast, delayedEpisode, enrichmentContext));
                    }
                }
            }
        }

        return new EnrichmentResults(results);
    }
}
