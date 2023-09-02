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

    public async Task<ResolvedPodcastEpisode> ResolveSpotifyUrl(Uri spotifyUrl)
    {
        var storedPodcasts = await _podcastRepository.GetAll().ToListAsync();
        var matchingPodcast = storedPodcasts.SingleOrDefault(x =>
            x.Episodes.Select(y => y.Urls.Spotify).Contains(spotifyUrl));
        var matchingEpisode = matchingPodcast?.Episodes
            .SingleOrDefault(x => x.Urls.Spotify == spotifyUrl);
        return new ResolvedPodcastEpisode(
            matchingPodcast ?? throw new InvalidOperationException($"Missing matching podcast for '{spotifyUrl}'."),
            matchingEpisode ?? throw new InvalidOperationException($"Missing matching episode for '{spotifyUrl}'."));
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
                        var post = true;
                        // test for whether episode could be waiting for YouTube item
                        if (matchingEpisode.Urls.YouTube == null &&
                            !string.IsNullOrWhiteSpace(matchingPodcast!.YouTubePublishingDelayTimeSpan))
                        {
                            var timeSpan = TimeSpan.Parse(matchingPodcast.YouTubePublishingDelayTimeSpan);
                            if (matchingEpisode.Release.Add(timeSpan) > DateTime.UtcNow)
                            {
                                post = false;
                            }
                        }

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