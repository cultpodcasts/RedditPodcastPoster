using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeResolver(IPodcastRepository podcastRepository, ILogger<EpisodeResolver> logger)
    : IEpisodeResolver
{
    private readonly ILogger<EpisodeResolver> _logger = logger;

    public async Task<PodcastEpisode> ResolveServiceUrl(Uri url)
    {
        var matchingPodcast = await podcastRepository.GetBy(x =>
            x.Episodes.Select(y => y.Urls.Spotify).Contains(url) ||
            x.Episodes.Select(y => y.Urls.Apple).Contains(url) ||
            x.Episodes.Select(y => y.Urls.YouTube).Contains(url)
        );
        var matchingEpisode = matchingPodcast?.Episodes
            .SingleOrDefault(x => x.Urls.Spotify == url || x.Urls.Apple == url || x.Urls.YouTube == url);
        return new PodcastEpisode(
            matchingPodcast ?? throw new InvalidOperationException($"Missing matching podcast for '{url}'."),
            matchingEpisode ?? throw new InvalidOperationException($"Missing matching episode for '{url}'."));
    }
}