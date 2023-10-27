using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

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
}