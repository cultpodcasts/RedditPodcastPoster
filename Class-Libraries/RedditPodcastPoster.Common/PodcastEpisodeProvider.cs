using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using LegacyPodcast = RedditPodcastPoster.Models.Podcast;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common;

public class PodcastEpisodeProvider(
    IPodcastRepository repository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodeProvider> logger
) : IPodcastEpisodeProvider
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method}, {execMethod} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            nameof(repository.GetPodcastIdsWithUntweetedReleasedSince),
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            repository.GetPodcastIdsWithUntweetedReleasedSince,
            podcastEpisodeFilter.GetMostRecentUntweetedEpisodes,
            youTubeRefreshed,
            spotifyRefreshed);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetUntweetedPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetUntweetedPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            podcastId,
            podcastEpisodeFilter.GetMostRecentUntweetedEpisodes);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation("Exec {method}, {execMethod} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            nameof(repository.GetPodcastIdsWithBlueskyReadyReleasedSince),
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            repository.GetPodcastIdsWithBlueskyReadyReleasedSince,
            podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes,
            youTubeRefreshed,
            spotifyRefreshed);
    }

    public async Task<IEnumerable<PodcastEpisodeV2>> GetBlueskyReadyPodcastEpisodes(Guid podcastId)
    {
        logger.LogInformation("Exec {method}, podcast-id: {podcastId} init. Tweet-days: '{tweetDays}'",
            nameof(GetBlueskyReadyPodcastEpisodes),
            podcastId,
            _postingCriteria.TweetDays);
        return await GetPodcastEpisodes(
            podcastId,
            podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes);
    }

    private async Task<IEnumerable<PodcastEpisodeV2>> GetPodcastEpisodes(
        Func<DateTime, Task<IEnumerable<Guid>>> findPodcast,
        Func<Podcast, bool, bool, int, Task<IEnumerable<PodcastEpisodeV2>>> filterEpisodes,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var podcastEpisodes = new List<PodcastEpisodeV2>();
        var dateTime = DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays);

        var untweetedPodcastIds = await findPodcast(dateTime);

        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            if (podcast == null)
            {
                logger.LogError("Podcast with id '{UntweetedPodcastId}' not found.", untweetedPodcastId);
            }
            else
            {
                var filtered = await filterEpisodes(ToV2Podcast(podcast), youTubeRefreshed, spotifyRefreshed, _postingCriteria.TweetDays);
                podcastEpisodes.AddRange(filtered);
            }
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private async Task<IEnumerable<PodcastEpisodeV2>> GetPodcastEpisodes(
        Guid podcastId,
        Func<Podcast, int, Task<IEnumerable<PodcastEpisodeV2>>> filterEpisodes)
    {
        var podcastEpisodes = new List<PodcastEpisodeV2>();
        var podcast = await repository.GetPodcast(podcastId);
        if (podcast == null)
        {
            logger.LogError("Podcast with id '{UntweetedPodcastId}' not found.", podcastId);
        }
        else
        {
            var filtered = await filterEpisodes(ToV2Podcast(podcast), _postingCriteria.TweetDays);
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }

    private static Podcast ToV2Podcast(LegacyPodcast podcast)
    {
        return new Podcast
        {
            Id = podcast.Id,
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            Publisher = podcast.Publisher,
            Bundles = podcast.Bundles,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            MinimumDuration = podcast.MinimumDuration,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            SpotifyMarket = podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle,
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            DefaultSubject = podcast.DefaultSubject,
            SearchTerms = podcast.SearchTerms,
            KnownTerms = podcast.KnownTerms,
            FileKey = podcast.FileKey,
            Timestamp = podcast.Timestamp
        };
    }
}