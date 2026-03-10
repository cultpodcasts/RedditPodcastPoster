namespace RedditPodcastPoster.Models.Extensions;

/// <summary>
/// Extension methods for converting between PodcastEpisode and PodcastEpisodeV2.
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

    /// <summary>
    /// Converts a legacy PodcastEpisode to V2 PodcastEpisode.
    /// Note: This should only be used during transition - prefer loading from V2 repositories.
    /// </summary>
    public static PodcastEpisodeV2 ToV2(this PodcastEpisode legacyPodcastEpisode)
    {
        var v2Podcast = legacyPodcastEpisode.Podcast.ToV2Podcast();
        var v2Episode = ToV2Episode(legacyPodcastEpisode.Podcast, legacyPodcastEpisode.Episode);
        return new PodcastEpisodeV2(v2Podcast, v2Episode);
    }

    private static Models.V2.Episode ToV2Episode(Podcast podcast, Episode episode)
    {
        return new Models.V2.Episode
        {
            Id = episode.Id,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Removed = episode.Removed,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Subjects = episode.Subjects ?? [],
            SearchTerms = episode.SearchTerms,
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            Language = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}
