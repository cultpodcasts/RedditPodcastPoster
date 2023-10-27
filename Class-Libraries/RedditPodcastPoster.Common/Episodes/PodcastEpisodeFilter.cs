using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodeFilter : IPodcastEpisodeFilter
{
    private readonly ILogger<PodcastEpisodeFilter> _logger;

    public PodcastEpisodeFilter(ILogger<PodcastEpisodeFilter> logger)
    {
        _logger = logger;
    }

    public IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(IList<Podcast> podcasts, DateTime since)
    {
        var matchingPodcasts = podcasts.Where(podcast =>
            podcast.Episodes.Any(episode =>
                episode.Release >= since && episode is { Posted: false, Ignored: false, Removed: false }));
        var resolvedPodcastEpisodeSince = new List<PodcastEpisode>();
        foreach (var matchingPodcast in matchingPodcasts)
        {
            var matchingEpisodes = matchingPodcast.Episodes
                .Where(episode =>
                    episode.Release >= since && episode is { Posted: false, Ignored: false, Removed: false });
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

    public PodcastEpisode? GetMostRecentUntweetedEpisode(IList<Podcast> podcasts)
    {
        var podcastEpisode =
            podcasts
                .SelectMany(p => p.Episodes.Select(e => new PodcastEpisode(p, e)))
                .Where(x =>
                    x.Episode.Release >= DateTime.UtcNow.Date.AddHours(-24) &&
                    x.Episode is { Removed: false, Ignored: false, Tweeted: false } &&
                    (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                    !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
                .MaxBy(x => x.Episode.Release);
        if (podcastEpisode?.Podcast == null)
        {
            _logger.LogInformation("No Podcast-Episode found to Tweet.");
            return null;
        }

        return podcastEpisode;
    }
}