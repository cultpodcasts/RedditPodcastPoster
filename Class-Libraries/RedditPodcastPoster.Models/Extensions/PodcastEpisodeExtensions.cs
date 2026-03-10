namespace RedditPodcastPoster.Models.Extensions;

/// <summary>
/// Extension methods for model conversion.
/// </summary>
public static class PodcastEpisodeExtensions
{
    /// <summary>
    /// Converts a legacy Podcast to V2 Podcast (without episodes).
    /// </summary>
    public static Models.V2.Podcast ToV2Podcast(this Podcast legacyPodcast)
    {
        return new Models.V2.Podcast
        {
            Id = legacyPodcast.Id,
            Name = legacyPodcast.Name,
            Language = legacyPodcast.Language,
            Removed = legacyPodcast.Removed,
            Publisher = legacyPodcast.Publisher,
            Bundles = legacyPodcast.Bundles,
            IndexAllEpisodes = legacyPodcast.IndexAllEpisodes,
            IgnoreAllEpisodes = legacyPodcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = legacyPodcast.BypassShortEpisodeChecking,
            MinimumDuration = legacyPodcast.MinimumDuration,
            ReleaseAuthority = legacyPodcast.ReleaseAuthority,
            PrimaryPostService = legacyPodcast.PrimaryPostService,
            SpotifyId = legacyPodcast.SpotifyId,
            SpotifyMarket = legacyPodcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = legacyPodcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = legacyPodcast.AppleId,
            YouTubeChannelId = legacyPodcast.YouTubeChannelId,
            YouTubePlaylistId = legacyPodcast.YouTubePlaylistId,
            YouTubePublicationOffset = legacyPodcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = legacyPodcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = legacyPodcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = legacyPodcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = legacyPodcast.TwitterHandle,
            BlueskyHandle = legacyPodcast.BlueskyHandle,
            HashTag = legacyPodcast.HashTag,
            EnrichmentHashTags = legacyPodcast.EnrichmentHashTags,
            TitleRegex = legacyPodcast.TitleRegex,
            DescriptionRegex = legacyPodcast.DescriptionRegex,
            EpisodeMatchRegex = legacyPodcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = legacyPodcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = legacyPodcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = legacyPodcast.IgnoredSubjects,
            DefaultSubject = legacyPodcast.DefaultSubject,
            SearchTerms = legacyPodcast.SearchTerms,
            KnownTerms = legacyPodcast.KnownTerms,
            FileKey = legacyPodcast.FileKey,
            Timestamp = legacyPodcast.Timestamp
        };
    }
}
