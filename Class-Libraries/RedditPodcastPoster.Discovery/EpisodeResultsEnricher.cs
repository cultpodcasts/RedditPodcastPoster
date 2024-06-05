using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultsEnricher(
    IEpisodeResultEnricher episodeResultEnricher,
    IPodcastRepository podcastRepository,
    ILogger<EpisodeResultsEnricher> Logger) : IEpisodeResultsEnricher
{
    public async IAsyncEnumerable<EnrichedEpisodeResult> EnrichWithPodcastDetails(
        IEnumerable<EpisodeResult> episodeResults)
    {
        foreach (var episodeResult in episodeResults)
        {
            var podcasts = new List<Podcast>();
            if (episodeResult.PodcastIds.Spotify != null)
            {
                var spotifyPodcasts =
                    await podcastRepository.GetAllBy(x => x.SpotifyId == episodeResult.PodcastIds.Spotify)
                        .ToArrayAsync();
                var spotifyPodcast = GetCurrentIndexed(spotifyPodcasts);
                if (spotifyPodcast != null)
                {
                    podcasts.Add(spotifyPodcast);
                }
            }

            if (episodeResult.PodcastIds.Apple != null)
            {
                var applePodcasts =
                    await podcastRepository.GetAllBy(x => x.AppleId == episodeResult.PodcastIds.Apple)
                        .ToArrayAsync();
                var applePodcast = GetCurrentIndexed(applePodcasts);
                if (applePodcast != null)
                {
                    podcasts.Add(applePodcast);
                }
            }

            if (episodeResult.PodcastIds.YouTube != null)
            {
                var youTubePodcasts =
                    await podcastRepository.GetAllBy(x => x.YouTubeChannelId == episodeResult.PodcastIds.YouTube)
                        .ToArrayAsync();
                var youTubePodcast = GetCurrentIndexed(youTubePodcasts);
                if (youTubePodcast != null)
                {
                    podcasts.Add(youTubePodcast);
                }
            }

            yield return episodeResultEnricher.Enrich(episodeResult, podcasts.ToArray());
        }
    }

    private Podcast? GetCurrentIndexed(Podcast[] podcasts)
    {
        if (podcasts.Length == 0)
        {
            return null;
        }

        if (podcasts.Length == 1)
        {
            return podcasts.Single();
        }

        return podcasts.FirstOrDefault(x => x.IndexAllEpisodes || !string.IsNullOrEmpty(x.EpisodeIncludeTitleRegex));
    }
}