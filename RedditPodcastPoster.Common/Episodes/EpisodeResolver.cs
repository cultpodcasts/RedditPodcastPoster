using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeResolver : IEpisodeResolver
{
    private readonly ILogger<EpisodeResolver> _logger;
    private readonly IPodcastRepository _podcastRepository;

    public EpisodeResolver(IPodcastRepository podcastRepository, ILogger<EpisodeResolver> logger)
    {
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task<PodcastEpisode> ResolveServiceUrl(Uri url)
    {
        var storedPodcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcast = storedPodcasts.SingleOrDefault(x =>
            x.Episodes.Select(y => y.Urls.Spotify).Contains(url) ||
            x.Episodes.Select(y => y.Urls.Apple).Contains(url) ||
            x.Episodes.Select(y => y.Urls.YouTube).Contains(url));
        var matchingEpisode = matchingPodcast?.Episodes
            .SingleOrDefault(x => x.Urls.Spotify == url || x.Urls.Apple == url || x.Urls.YouTube == url);
        return new PodcastEpisode(
            matchingPodcast ?? throw new InvalidOperationException($"Missing matching podcast for '{url}'."),
            matchingEpisode ?? throw new InvalidOperationException($"Missing matching episode for '{url}'."));
    }

    public async Task<IEnumerable<PodcastEpisode>> ResolveSinceReleaseDate(DateTime since)
    {
        var storedPodcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcasts = storedPodcasts.Where(podcast =>
            podcast.Episodes.Any(episode =>
                episode.Release >= since && episode is {Posted: false, Ignored: false, Removed: false}));
        var resolvedPodcastEpisodeSince = new List<PodcastEpisode>();
        foreach (var matchingPodcast in matchingPodcasts)
        {
            var matchingEpisodes = matchingPodcast.Episodes
                .Where(episode =>
                    episode.Release >= since && episode is {Posted: false, Ignored: false, Removed: false});
            foreach (var matchingEpisode in matchingEpisodes)
            {
                var post = !matchingPodcast.IsDelayedYouTubePublishing(matchingEpisode);

                if (post)
                {
                    resolvedPodcastEpisodeSince.Add(new PodcastEpisode(matchingPodcast, matchingEpisode));
                }
            }
        }

        return resolvedPodcastEpisodeSince;
    }
}