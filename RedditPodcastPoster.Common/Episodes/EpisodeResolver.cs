using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Podcasts;

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

    public async Task<ResolvedPodcastEpisode> ResolveServiceUrl(Uri url)
    {
        var storedPodcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcast = storedPodcasts.SingleOrDefault(x =>
            x.Episodes.Select(y => y.Urls.Spotify).Contains(url) ||
            x.Episodes.Select(y => y.Urls.Apple).Contains(url) ||
            x.Episodes.Select(y => y.Urls.YouTube).Contains(url));
        var matchingEpisode = matchingPodcast?.Episodes
            .SingleOrDefault(x => x.Urls.Spotify == url || x.Urls.Apple == url || x.Urls.YouTube == url);
        return new ResolvedPodcastEpisode(
            matchingPodcast ?? throw new InvalidOperationException($"Missing matching podcast for '{url}'."),
            matchingEpisode ?? throw new InvalidOperationException($"Missing matching episode for '{url}'."));
    }

    public async Task<IEnumerable<ResolvedPodcastEpisode>> ResolveSinceReleaseDate(DateTime since)
    {
        var storedPodcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcasts = storedPodcasts.Where(x =>
            x.Episodes.Any(y => y.Release >= since));
        var resolvedPodcastEpisodeSince = new List<ResolvedPodcastEpisode>();
        foreach (var matchingPodcast in matchingPodcasts)
        {
            var matchingEpisodes = matchingPodcast?.Episodes
                .Where(x => x.Release >= since);
            if (matchingEpisodes != null)
            {
                foreach (var matchingEpisode in matchingEpisodes)
                {
                    if (matchingEpisode is {Posted: false, Ignored: false})
                    {
                        var post = !matchingPodcast!.IsDelayedYouTubePublishing(matchingEpisode);

                        if (post)
                        {
                            resolvedPodcastEpisodeSince.Add(
                                new ResolvedPodcastEpisode(matchingPodcast!, matchingEpisode));
                        }
                    }
                }
            }
        }

        return resolvedPodcastEpisodeSince;
    }
}