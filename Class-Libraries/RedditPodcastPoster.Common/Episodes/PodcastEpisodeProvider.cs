using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Implementation that provides podcast episodes from detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodeProvider(
    IPodcastRepositoryV2 podcastRepository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodeProvider> logger
) : IPodcastEpisodeProvider
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            _postingCriteria.TweetDays);

        var allPodcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastEpisodes = new List<PodcastEpisode>();

        foreach (var podcast in allPodcasts)
        {
            var filtered = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast,
                _postingCriteria.TweetDays);

            var validEpisodes = filtered.Where(pe =>
                EliminateItemsDueToIndexingErrors(pe, youTubeRefreshed, spotifyRefreshed));

            podcastEpisodes.AddRange(validEpisodes);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{podcastId}' not found.", podcastId);
            return Enumerable.Empty<PodcastEpisode>();
        }

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
            podcast,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            _postingCriteria.TweetDays);

        var allPodcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastEpisodes = new List<PodcastEpisode>();

        foreach (var podcast in allPodcasts)
        {
            var filtered = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
                podcast,
                _postingCriteria.TweetDays);

            var validEpisodes = filtered.Where(pe =>
                EliminateItemsDueToIndexingErrors(pe, youTubeRefreshed, spotifyRefreshed));

            podcastEpisodes.AddRange(validEpisodes);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisode>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            return Enumerable.Empty<PodcastEpisode>();
        }

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
            podcast,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private static bool EliminateItemsDueToIndexingErrors(
        PodcastEpisode podcastEpisode,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var eliminateForYouTube =
            podcastEpisode.Podcast.ReleaseAuthority == Service.YouTube &&
            !youTubeRefreshed &&
            podcastEpisode.Episode.Urls.YouTube == null;

        var eliminateForSpotify =
            podcastEpisode.Podcast.ReleaseAuthority == Service.Spotify &&
            !spotifyRefreshed &&
            podcastEpisode.Episode.Urls.Spotify == null;

        return !(eliminateForYouTube || eliminateForSpotify);
    }
}

