using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodeFilter(
    IOptions<DelayedYouTubePublication> delayedYouTubePublicationSettings,
    ILogger<PodcastEpisodeFilter> logger)
    : IPodcastEpisodeFilter
{
    private readonly DelayedYouTubePublication _delayedYouTubePublicationSettings =
        delayedYouTubePublicationSettings.Value;

    public IEnumerable<PodcastEpisode> GetNewEpisodesReleasedSince(
        IEnumerable<Podcast> podcasts,
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
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

        return resolvedPodcastEpisodeSince.Where(x =>
            EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed));
    }

    public bool IsRecentlyExpiredDelayedPublishing(Podcast podcast, Episode episode)
    {
        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay > TimeSpan.Zero)
        {
            var isRecentlyExpiredDelayedPublishing =
                episode.Release.Add(youTubePublishingDelay) <= DateTime.UtcNow
                && episode.Release.Add(
                    youTubePublishingDelay.Add(_delayedYouTubePublicationSettings
                        .EvaluationThreshold)) >= DateTime.UtcNow;
            return isRecentlyExpiredDelayedPublishing;
        }

        return false;
    }

    public IEnumerable<PodcastEpisode> GetMostRecentUntweetedEpisodes(
        IEnumerable<Podcast> podcasts,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        int numberOfDays = 1)
    {
        var podcastEpisodes =
            podcasts
                .SelectMany(p => p.Episodes.Select(e => new PodcastEpisode(p, e)))
                .Where(x =>
                    x.Episode.Release >= DateTime.UtcNow.Date.AddDays(-1 * numberOfDays) &&
                    x.Episode is {Removed: false, Ignored: false, Tweeted: false} &&
                    (x.Episode.Urls.YouTube != null || x.Episode.Urls.Spotify != null) &&
                    !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
                .Where(x =>
                    EliminateItemsDueToIndexingErrors(x, youTubeRefreshed, spotifyRefreshed))
                .OrderByDescending(x => x.Episode.Release);
        if (!podcastEpisodes.Any())
        {
            logger.LogInformation("No Podcast-Episode found to Tweet.");
        }

        return podcastEpisodes;
    }

    private bool IsReadyToPost(Podcast podcast, Episode episode, DateTime since)
    {
        if (episode.Posted || episode.Ignored || episode.Removed)
        {
            return false;
        }

        var youTubePublishingDelay = podcast.YouTubePublishingDelay();
        if (podcast.ReleaseAuthority == Service.YouTube)
        {
            since += youTubePublishingDelay;
        }

        if (episode.Release >= since)
        {
            if ((!string.IsNullOrWhiteSpace(podcast.SpotifyId) && episode.Urls.Spotify != null) ||
                (string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                 podcast.AppleId != null && episode.Urls.Apple != null) || (podcast.AppleId == null &&
                                                                            !string.IsNullOrWhiteSpace(
                                                                                podcast.YouTubeChannelId) &&
                                                                            episode.Urls.YouTube != null) ||
                string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
            {
                return true;
            }
        }

        var releasedSince = episode.Release >= since && episode.Release - DateTime.UtcNow < youTubePublishingDelay;

        return releasedSince || IsRecentlyExpiredDelayedPublishing(podcast, episode);
    }

    private bool EliminateItemsDueToIndexingErrors(
        PodcastEpisode podcastEpisode,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        if (IsRecentlyExpiredDelayedPublishing(podcastEpisode.Podcast, podcastEpisode.Episode) ||
            !youTubeRefreshed)
        {
            logger.LogInformation(
                $"{nameof(EliminateItemsDueToIndexingErrors)} Eliminating episode with episode-id '{podcastEpisode.Episode.Id}' and episode-title '{podcastEpisode.Episode.Title}' from podcast with podcast-id '{podcastEpisode.Podcast.Id}' and podcast-name '{podcastEpisode.Podcast.Name}' due to '{nameof(youTubeRefreshed)}'='{youTubeRefreshed}'.");
            return false;
        }

        return true;
    }
}