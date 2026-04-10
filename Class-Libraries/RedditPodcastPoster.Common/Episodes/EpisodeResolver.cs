using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeResolver(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<EpisodeResolver> logger)
    : IEpisodeResolver
{
    private readonly ILogger<EpisodeResolver> _logger = logger;

    public async Task<PodcastEpisode> ResolveServiceUrl(Uri url)
    {
        var matchingEpisode = await episodeRepository.GetBy(x =>
            x.Urls.Spotify == url || x.Urls.Apple == url || x.Urls.YouTube == url);

        if (matchingEpisode == null)
        {
            _logger.LogError("Missing matching episode for '{Url}'.", url);
            throw new InvalidOperationException($"Missing matching episode for '{url}'.");
        }

        var matchingPodcast = await podcastRepository.GetPodcast(matchingEpisode.PodcastId);
        if (matchingPodcast == null)
        {
            _logger.LogError("Missing matching podcast for '{Url}' and podcast-id '{PodcastId}'.", url, matchingEpisode.PodcastId);
            throw new InvalidOperationException($"Missing matching podcast for '{url}'.");
        }

        return new PodcastEpisode(matchingPodcast, matchingEpisode);
    }
}
