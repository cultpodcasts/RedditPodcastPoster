namespace RedditPodcastPoster.Models.Extensions;

/// <summary>
/// Extension methods for converting between PodcastEpisode and PodcastEpisodeV2.
/// </summary>
public static class PodcastEpisodeExtensions
{
    /// <summary>
    /// Converts a V2 Episode to legacy Episode.
    /// </summary>
    public static Episode ToLegacyEpisode(this Models.V2.Episode v2Episode)
    {
        return new Episode
        {
            Id = v2Episode.Id,
            Title = v2Episode.Title,
            Description = v2Episode.Description,
            Release = v2Episode.Release,
            Length = v2Episode.Length,
            Explicit = v2Episode.Explicit,
            Posted = v2Episode.Posted,
            Tweeted = v2Episode.Tweeted,
            BlueskyPosted = v2Episode.BlueskyPosted,
            Ignored = v2Episode.Ignored,
            Removed = v2Episode.Removed,
            SpotifyId = v2Episode.SpotifyId,
            AppleId = v2Episode.AppleId,
            YouTubeId = v2Episode.YouTubeId,
            Urls = v2Episode.Urls,
            Subjects = v2Episode.Subjects,
            SearchTerms = v2Episode.SearchTerms,
            Language = v2Episode.SearchLanguage,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }

    /// <summary>
    /// Converts a V2 Podcast to legacy Podcast (without episodes).
    /// </summary>
    public static Podcast ToLegacyPodcast(this Models.V2.Podcast v2Podcast)
    {
        return new Podcast(v2Podcast.Id)
        {
            Name = v2Podcast.Name,
            Language = v2Podcast.Language,
            Removed = v2Podcast.Removed,
            Publisher = v2Podcast.Publisher,
            Bundles = v2Podcast.Bundles,
            IndexAllEpisodes = v2Podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = v2Podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = v2Podcast.BypassShortEpisodeChecking,
            MinimumDuration = v2Podcast.MinimumDuration,
            ReleaseAuthority = v2Podcast.ReleaseAuthority,
            PrimaryPostService = v2Podcast.PrimaryPostService,
            SpotifyId = v2Podcast.SpotifyId,
            SpotifyMarket = v2Podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = v2Podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = v2Podcast.AppleId,
            YouTubeChannelId = v2Podcast.YouTubeChannelId,
            YouTubePlaylistId = v2Podcast.YouTubePlaylistId,
            YouTubePublicationOffset = v2Podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = v2Podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = v2Podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = v2Podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = v2Podcast.TwitterHandle,
            BlueskyHandle = v2Podcast.BlueskyHandle,
            HashTag = v2Podcast.HashTag,
            EnrichmentHashTags = v2Podcast.EnrichmentHashTags,
            TitleRegex = v2Podcast.TitleRegex,
            DescriptionRegex = v2Podcast.DescriptionRegex,
            EpisodeMatchRegex = v2Podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = v2Podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = v2Podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = v2Podcast.IgnoredSubjects,
            DefaultSubject = v2Podcast.DefaultSubject,
            SearchTerms = v2Podcast.SearchTerms,
            KnownTerms = v2Podcast.KnownTerms,
            FileKey = v2Podcast.FileKey,
            Timestamp = v2Podcast.Timestamp,
            Episodes = [] // Empty - episodes are detached in V2
        };
    }

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
    /// Converts a V2 PodcastEpisode to legacy PodcastEpisode.
    /// </summary>
    public static PodcastEpisode ToLegacy(this PodcastEpisodeV2 v2PodcastEpisode)
    {
        var legacyPodcast = v2PodcastEpisode.Podcast.ToLegacyPodcast();
        var legacyEpisode = v2PodcastEpisode.Episode.ToLegacyEpisode();
        return new PodcastEpisode(legacyPodcast, legacyEpisode);
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
            SearchLanguage = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}
