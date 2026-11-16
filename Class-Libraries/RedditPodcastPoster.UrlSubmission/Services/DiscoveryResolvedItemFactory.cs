using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Builds a non-podcast resolved item from a discovery result so a confirmed film / TV / news
/// submit has a title, duration and platform URLs. Spotify, Apple and YouTube are not
/// <see cref="StreamingService"/> values; those links are copied onto the stored item separately.
/// </summary>
public static class DiscoveryResolvedItemFactory
{
    public static ResolvedNonPodcastServiceItem From(
        DiscoveryResult discoveryResult,
        CategorisedItem categorisedItem)
    {
        var youtube = categorisedItem.ResolvedYouTubeItem;
        var spotify = categorisedItem.ResolvedSpotifyItem;
        var apple = categorisedItem.ResolvedAppleItem;
        var title = FirstNonEmpty(
            youtube?.EpisodeTitle,
            spotify?.EpisodeTitle,
            apple?.EpisodeTitle,
            discoveryResult.EpisodeName);
        var description = FirstNonEmpty(
            youtube?.EpisodeDescription,
            spotify?.EpisodeDescription,
            apple?.EpisodeDescription,
            discoveryResult.Description);
        var showName = FirstNonEmpty(
            youtube?.ShowName,
            spotify?.ShowName,
            apple?.ShowName,
            discoveryResult.ShowName);
        var publisher = FirstNonEmpty(youtube?.Publisher, spotify?.Publisher, apple?.Publisher);
        var image = youtube?.Image ?? spotify?.Image ?? apple?.Image ?? discoveryResult.ImageUrl;
        var duration = NonZero(youtube?.Duration) ?? NonZero(spotify?.Duration) ?? NonZero(apple?.Duration) ??
                       discoveryResult.Length;
        var release = NonDefault(youtube?.Release) ?? NonDefault(spotify?.Release) ?? NonDefault(apple?.Release) ??
                      (discoveryResult.Released == default ? null : discoveryResult.Released);

        return new ResolvedNonPodcastServiceItem(
            StreamingService.Vimeo,
            Url: null,
            Title: title,
            Description: description,
            Publisher: publisher,
            Image: image,
            Release: release,
            Duration: duration,
            KnownExplicit: youtube?.Explicit ?? spotify?.Explicit ?? apple?.Explicit,
            ShowName: showName);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static TimeSpan? NonZero(TimeSpan? value) =>
        value is { } duration && duration > TimeSpan.Zero ? duration : null;

    private static DateTime? NonDefault(DateTime? value) =>
        value is { } release && release != default ? release : null;
}