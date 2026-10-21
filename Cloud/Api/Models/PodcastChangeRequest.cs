using RedditPodcastPoster.Models.Podcasts;

namespace Api.Models;

public class PodcastChangeRequest
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }

    public string? Language { get; set; }

    public bool? Removed { get; set; }

    public bool? IndexAllEpisodes { get; set; }

    public bool? BypassShortEpisodeChecking { get; set; }

    public bool? AlwaysPromoteAsHero { get; set; }

    public Service? ReleaseAuthority { get; set; }

    public bool? UnsetReleaseAuthority { get; set; }

    public Service? PrimaryPostService { get; set; }

    public bool? UnsetPrimaryPostService { get; set; }

    public string? SpotifyId { get; set; }

    public long? AppleId { get; set; }

    public bool? NullAppleId { get; set; }

    public string? YouTubePublishingDelayTimeSpan { get; set; }

    public bool? SkipEnrichingFromYouTube { get; set; }

    public string? TwitterHandle { get; set; }

    public string? BlueskyHandle { get; set; }

    public string[]? EnrichmentHashTags { get; set; }

    public string? HashTag { get; set; }

    public string? TitleRegex { get; set; }

    public string? DescriptionRegex { get; set; }

    public string? EpisodeMatchRegex { get; set; }

    public string? EpisodeIncludeTitleRegex { get; set; }

    public string? DefaultSubject { get; set; }

    public bool? IgnoreAllEpisodes { get; set; }

    public string? YouTubeChannelId { get; set; }

    public string? YouTubePlaylistId { get; set; }

    public string[]? IgnoredAssociatedSubjects { get; set; }

    public string[]? IgnoredSubjects { get; set; }

    public string[]? KnownTerms { get; set; }

    public string? MinimumDuration { get; set; }
}
