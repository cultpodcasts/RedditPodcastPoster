using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Dtos.Extensions;

public static class PodcastExtension
{
    public static Podcast ToDto(this DomainPodcast podcast)
    {
        return new Podcast
        {
            Id = podcast.Id,
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            AppleId = podcast.AppleId,
            YouTubePublishingDelayTimeSpan = podcast.YouTubePublicationOffset.HasValue
                ? TimeSpan.FromTicks(podcast.YouTubePublicationOffset.Value).ToString("g")
                : string.Empty,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle ?? string.Empty,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            DefaultSubject = podcast.DefaultSubject,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes ?? false,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            KnownTerms = podcast.KnownTerms,
            MinimumDuration = podcast.MinimumDuration?.ToString(),
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags
        };
    }
}
