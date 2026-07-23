using Microsoft.Extensions.Logging;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Api.Models;

namespace Api.Services.Podcasts;

public class PodcastChangeApplier(ILogger<PodcastChangeApplier> logger)
{
    public void Apply(DomainPodcast podcast, PodcastChangeRequest podcastChangeRequest)
    {
        if (podcastChangeRequest.Removed != null)
        {
            podcast.Removed = podcastChangeRequest.Removed;
        }

        if (podcastChangeRequest.IndexAllEpisodes != null)
        {
            podcast.IndexAllEpisodes = podcastChangeRequest.IndexAllEpisodes.Value;
        }

        if (podcastChangeRequest.BypassShortEpisodeChecking != null)
        {
            podcast.BypassShortEpisodeChecking = podcastChangeRequest.BypassShortEpisodeChecking.Value;
        }

        if (podcastChangeRequest.MinimumDuration != null)
        {
            if (string.IsNullOrWhiteSpace(podcastChangeRequest.MinimumDuration))
            {
                podcast.MinimumDuration = null;
            }
            else
            {
                podcast.MinimumDuration = TimeSpan.TryParse(podcastChangeRequest.MinimumDuration, out var duration)
                    ? duration
                    : null;
                if (podcast.MinimumDuration == null)
                {
                    logger.LogWarning("Invalid minimum-duration format; '{minimumDuration}'.",
                        podcastChangeRequest.MinimumDuration);
                }
            }
        }

        if (podcastChangeRequest.ReleaseAuthority != null)
        {
            podcast.ReleaseAuthority = podcastChangeRequest.ReleaseAuthority.Value;
        }

        if (podcastChangeRequest.UnsetReleaseAuthority != null && podcastChangeRequest.UnsetReleaseAuthority.Value)
        {
            podcast.ReleaseAuthority = null;
        }

        if (podcastChangeRequest.PrimaryPostService != null)
        {
            podcast.PrimaryPostService = podcastChangeRequest.PrimaryPostService.Value;
        }

        if (podcastChangeRequest.UnsetPrimaryPostService != null && podcastChangeRequest.UnsetPrimaryPostService.Value)
        {
            podcast.PrimaryPostService = null;
        }

        if (podcastChangeRequest.SpotifyId != null)
        {
            podcast.SpotifyId = podcastChangeRequest.SpotifyId == string.Empty
                ? string.Empty
                : podcastChangeRequest.SpotifyId;
        }

        if (podcastChangeRequest.AppleId != null ||
            (podcastChangeRequest.NullAppleId.HasValue && podcastChangeRequest.NullAppleId.Value))
        {
            if (podcastChangeRequest.NullAppleId.HasValue && podcastChangeRequest.NullAppleId.Value)
            {
                podcast.AppleId = null;
            }
            else if (podcastChangeRequest.AppleId.HasValue)
            {
                podcast.AppleId = podcastChangeRequest.AppleId.Value;
            }
            else
            {
                throw new InvalidOperationException("Indeterminate state of apple-id");
            }
        }

        if (podcastChangeRequest.YouTubePublishingDelayTimeSpan != null)
        {
            if (podcastChangeRequest.YouTubePublishingDelayTimeSpan == string.Empty)
            {
                podcast.YouTubePublicationOffset = null;
            }
            else
            {
                podcast.YouTubePublicationOffset =
                    TimeSpan.Parse(podcastChangeRequest.YouTubePublishingDelayTimeSpan).Ticks;
            }
        }

        if (podcastChangeRequest.YouTubePlaylistId != null)
        {
            podcast.YouTubePlaylistId = podcastChangeRequest.YouTubePlaylistId;
        }

        if (podcastChangeRequest.SkipEnrichingFromYouTube != null)
        {
            podcast.SkipEnrichingFromYouTube = podcastChangeRequest.SkipEnrichingFromYouTube.Value;
        }

        if (podcastChangeRequest.TwitterHandle != null)
        {
            podcast.TwitterHandle = podcastChangeRequest.TwitterHandle;
        }

        if (podcastChangeRequest.BlueskyHandle != null)
        {
            podcast.BlueskyHandle = string.IsNullOrWhiteSpace(podcastChangeRequest.BlueskyHandle)
                ? null
                : podcastChangeRequest.BlueskyHandle;
        }

        if (podcastChangeRequest.EnrichmentHashTags != null)
        {
            podcast.EnrichmentHashTags = !podcastChangeRequest.EnrichmentHashTags.Any()
                ? null
                : podcastChangeRequest.EnrichmentHashTags.Select(x => x.Trim()).ToArray();
        }

        if (podcastChangeRequest.HashTag != null)
        {
            podcast.HashTag = podcastChangeRequest.HashTag == string.Empty ? null : podcastChangeRequest.HashTag.Trim();
        }

        if (podcastChangeRequest.TitleRegex != null)
        {
            podcast.TitleRegex = podcastChangeRequest.TitleRegex;
        }

        if (podcastChangeRequest.DescriptionRegex != null)
        {
            podcast.DescriptionRegex = podcastChangeRequest.DescriptionRegex;
        }

        if (podcastChangeRequest.EpisodeMatchRegex != null)
        {
            podcast.EpisodeMatchRegex = podcastChangeRequest.EpisodeMatchRegex;
        }

        if (podcastChangeRequest.EpisodeIncludeTitleRegex != null)
        {
            podcast.EpisodeIncludeTitleRegex = podcastChangeRequest.EpisodeIncludeTitleRegex;
            podcast.IndexAllEpisodes = false;
        }

        if (podcastChangeRequest.DefaultSubject != null)
        {
            podcast.DefaultSubject = podcastChangeRequest.DefaultSubject == string.Empty
                ? null
                : podcastChangeRequest.DefaultSubject;
        }

        if (podcastChangeRequest.IgnoreAllEpisodes != null)
        {
            if (podcastChangeRequest.IgnoreAllEpisodes.HasValue && podcastChangeRequest.IgnoreAllEpisodes.Value)
            {
                podcast.IgnoreAllEpisodes = true;
            }
            else
            {
                podcast.IgnoreAllEpisodes = null;
            }
        }

        if (podcastChangeRequest.IgnoredSubjects != null)
        {
            podcast.IgnoredSubjects = podcastChangeRequest.IgnoredSubjects;
        }

        if (podcastChangeRequest.IgnoredAssociatedSubjects != null)
        {
            podcast.IgnoredAssociatedSubjects = podcastChangeRequest.IgnoredAssociatedSubjects;
        }

        if (podcastChangeRequest.Language != null)
        {
            podcast.Language = podcastChangeRequest.Language == string.Empty ? null : podcastChangeRequest.Language;
        }

        if (podcastChangeRequest.KnownTerms != null)
        {
            podcast.KnownTerms = podcastChangeRequest.KnownTerms.Length > 0 ? podcastChangeRequest.KnownTerms : null;
        }
    }
}
