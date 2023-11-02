using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodeFilter : IPodcastEpisodeFilter
{
    private readonly DelayedYouTubePublication _delayedYouTubePublicationSettings;
    private readonly ILogger<PodcastEpisodeFilter> _logger;

    public PodcastEpisodeFilter(
        IOptions<DelayedYouTubePublication> delayedYouTubePublicationSettings,
        ILogger<PodcastEpisodeFilter> logger)
    {
        _logger = logger;
        _delayedYouTubePublicationSettings = delayedYouTubePublicationSettings.Value;
    }

    public IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(IList<Podcast> podcasts, DateTime since)
    {
        var matchingPodcasts = podcasts.Where(podcast =>
            podcast.Episodes.Any(episode => IsReadyToPost(podcast, episode, since)));
        var resolvedPodcastEpisodeSince = new List<PodcastEpisode>();
        foreach (var matchingPodcast in matchingPodcasts)
        {
            var matchingEpisodes = matchingPodcast.Episodes
                .Where(episode => IsReadyToPost(matchingPodcast, episode, since));
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
                    x.Episode is {Removed: false, Ignored: false, Tweeted: false} &&
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

    public bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode)
    {
        var isRecentlyExpiredDelayedPublishing =
            episode.Release.Add(podcast.YouTubePublishingDelay()!.Value) <= DateTime.UtcNow
            && episode.Release.Add(
                podcast.YouTubePublishingDelay()!.Value.Add(_delayedYouTubePublicationSettings
                    .EvaluationThreshold)) >= DateTime.UtcNow;
        if (isRecentlyExpiredDelayedPublishing)
        {
            _logger.LogInformation($"Considering episode '{episode.Title}' to have expired it's delayed publishing.");
        }

        return isRecentlyExpiredDelayedPublishing;
    }

    private bool IsReadyToPost(Podcast podcast, Episode episode, DateTime since)
    {
        return (episode.Release >= since || IsRecentlyExpiredDelayedPublishing(podcast, episode)) &&
               episode is {Posted: false, Ignored: false, Removed: false};
    }
}