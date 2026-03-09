using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 implementation that provides podcast episodes from detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodeProviderV2(
    IPodcastRepositoryV2 podcastRepository,
    IPodcastEpisodeFilterV2 podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodeProviderV2> logger
) : IPodcastEpisodeProviderV2
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            _postingCriteria.TweetDays);

        var allPodcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastEpisodes = new List<PodcastEpisodeV2>();

        foreach (var podcast in allPodcasts)
        {
            var filtered = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast.Id,
                _postingCriteria.TweetDays);

            var validEpisodes = filtered.Where(pe =>
                EliminateItemsDueToIndexingErrors(pe, youTubeRefreshed, spotifyRefreshed));

            podcastEpisodes.AddRange(validEpisodes);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
            podcastId,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            _postingCriteria.TweetDays);

        var allPodcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastEpisodes = new List<PodcastEpisodeV2>();

        foreach (var podcast in allPodcasts)
        {
            var filtered = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
                podcast.Id,
                _postingCriteria.TweetDays);

            var validEpisodes = filtered.Where(pe =>
                EliminateItemsDueToIndexingErrors(pe, youTubeRefreshed, spotifyRefreshed));

            podcastEpisodes.AddRange(validEpisodes);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);

        var podcastEpisodes = await podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes(
            podcastId,
            _postingCriteria.TweetDays);

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private bool EliminateItemsDueToIndexingErrors(
        PodcastEpisodeV2 podcastEpisode,
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