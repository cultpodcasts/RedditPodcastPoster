using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Applying;

public sealed class EpisodePlatformApplier : IEpisodePlatformApplier
{
    public bool ApplyFillMissing(Episode target, EpisodePlatformPatch patch)
    {
        var updated = false;

        if (patch.Link is { } link)
        {
            updated |= ApplyFillMissingLink(target, link);
        }

        if (patch.Description is { } description)
        {
            updated |= ApplyFillMissingDescription(target, description);
        }

        return updated;
    }

    public bool ApplyFillMissingRelease(Episode target, DateTime release)
    {
        if (target.Release == release)
        {
            return false;
        }

        target.Release = release;
        return true;
    }

    private static bool ApplyFillMissingLink(Episode target, PlatformLink link)
    {
        return link.Service switch
        {
            Service.Spotify => ApplySpotifyLink(target, link),
            Service.Apple => ApplyAppleLink(target, link),
            Service.YouTube => ApplyYouTubeLink(target, link),
            _ => false
        };
    }

    private static bool ApplySpotifyLink(Episode target, PlatformLink link)
    {
        var updated = EpisodeServicePresence.TryFillMissing(
            target, ServiceKeys.Spotify, link.Url, link.Image);
        if (string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(target)) &&
            !string.IsNullOrWhiteSpace(link.Id))
        {
            EpisodeServicePresence.SetSpotifyIdentity(target, link.Id);
            updated = true;
        }

        return updated;
    }

    private static bool ApplyAppleLink(Episode target, PlatformLink link)
    {
        var updated = EpisodeServicePresence.TryFillMissing(
            target, ServiceKeys.Apple, link.Url, link.Image);
        if (EpisodeServicePresence.AppleEpisodeId(target) is null &&
            !string.IsNullOrWhiteSpace(link.Id) &&
            long.TryParse(link.Id, out var appleId))
        {
            EpisodeServicePresence.SetAppleIdentity(target, appleId);
            updated = true;
        }

        return updated;
    }

    private static bool ApplyYouTubeLink(Episode target, PlatformLink link)
    {
        var updated = EpisodeServicePresence.TryFillMissing(
            target, ServiceKeys.YouTube, link.Url, link.Image);
        if (string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(target)) &&
            !string.IsNullOrWhiteSpace(link.Id))
        {
            EpisodeServicePresence.SetYouTubeIdentity(target, link.Id);
            updated = true;
        }

        return updated;
    }

    private static bool ApplyFillMissingDescription(Episode target, string description)
    {
        if (target.Description.EndsWith("...") &&
            target.Description.Length < description.Length)
        {
            target.Description = description;
            return true;
        }

        return false;
    }
}
