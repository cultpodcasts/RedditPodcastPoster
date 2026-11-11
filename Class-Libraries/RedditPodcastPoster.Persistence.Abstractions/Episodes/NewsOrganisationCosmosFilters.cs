using System.Linq.Expressions;
using RedditPodcastPoster.Models.News;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates for news organisations' YouTube channels (#1370). A news organisation holds a channel as its
/// main <c>indexing.youTubeChannelId</c> or as one of its own <c>additionalYouTubeChannels[]</c>, so every owner lookup,
/// uniqueness check and channel-wide claim check that finds a news organisation by channel must use
/// <see cref="HoldsYouTubeChannel"/> (or <see cref="OtherHoldsYouTubeChannel"/>) and not match the main channel alone.
/// A TV show only ever holds its main channel, so TV-show queries keep their own main-channel predicate.
/// The SQL these translate to is pinned by <c>NewsOrganisationCosmosFilterTranslationRules</c>.
/// </summary>
public static class NewsOrganisationCosmosFilters
{
    /// <summary>
    /// The news organisation holds <paramref name="channelId"/> as its main channel or as one of its additional
    /// channels (whole channel or playlist-scoped; callers that care about channel-wide claims check that in memory).
    /// </summary>
    public static Expression<Func<NewsOrganisation, bool>> HoldsYouTubeChannel(string channelId) =>
        x => (x.Indexing != null && x.Indexing.YouTubeChannelId == channelId) ||
             (x.AdditionalYouTubeChannels != null &&
              x.AdditionalYouTubeChannels.Any(c => c.YouTubeChannelId == channelId));

    /// <summary>
    /// <see cref="HoldsYouTubeChannel"/> for any news organisation other than <paramref name="excludedId"/> (the
    /// organisation being edited), for the uniqueness and channel-wide claim checks.
    /// </summary>
    public static Expression<Func<NewsOrganisation, bool>> OtherHoldsYouTubeChannel(Guid excludedId, string channelId) =>
        x => x.Id != excludedId &&
             ((x.Indexing != null && x.Indexing.YouTubeChannelId == channelId) ||
              (x.AdditionalYouTubeChannels != null &&
               x.AdditionalYouTubeChannels.Any(c => c.YouTubeChannelId == channelId)));

    /// <summary>
    /// The news organisation has indexing settings or additional YouTube channels, so it can own a discovered episode.
    /// </summary>
    public static Expression<Func<NewsOrganisation, bool>> HasIndexingOrAdditionalYouTubeChannels() =>
        x => x.Indexing != null || x.AdditionalYouTubeChannels != null;
}