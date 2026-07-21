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
        var updated = false;

        if (target.Urls.Spotify == null && link.Url != null)
        {
            target.Urls.Spotify = link.Url;
            updated = true;
        }

        if (target.Images?.Spotify == null && link.Image != null)
        {
            target.Images ??= new EpisodeImages();
            target.Images.Spotify = link.Image;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(target.SpotifyId) && !string.IsNullOrWhiteSpace(link.Id))
        {
            target.SpotifyId = link.Id;
            updated = true;
        }

        return updated;
    }

    private static bool ApplyAppleLink(Episode target, PlatformLink link)
    {
        var updated = false;

        if (target.Urls.Apple == null && link.Url != null)
        {
            target.Urls.Apple = link.Url;
            updated = true;
        }

        if (target.Images?.Apple == null && link.Image != null)
        {
            target.Images ??= new EpisodeImages();
            target.Images.Apple = link.Image;
            updated = true;
        }

        if (target.AppleId == null &&
            !string.IsNullOrWhiteSpace(link.Id) &&
            long.TryParse(link.Id, out var appleId))
        {
            target.AppleId = appleId;
            updated = true;
        }

        return updated;
    }

    private static bool ApplyYouTubeLink(Episode target, PlatformLink link)
    {
        var updated = false;

        if (target.Urls.YouTube == null && link.Url != null)
        {
            target.Urls.YouTube = link.Url;
            updated = true;
        }

        if (target.Images?.YouTube == null && link.Image != null)
        {
            target.Images ??= new EpisodeImages();
            target.Images.YouTube = link.Image;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(target.YouTubeId) && !string.IsNullOrWhiteSpace(link.Id))
        {
            target.YouTubeId = link.Id;
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
