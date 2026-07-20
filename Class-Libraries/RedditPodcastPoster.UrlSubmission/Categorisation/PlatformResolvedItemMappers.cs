using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public static class PlatformResolvedItemMappers
{
    public static CategorisedSpotifyItem FromPlatform(ResolvedSpotifyItem item) =>
        new(
            item.ShowId,
            item.EpisodeId,
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Explicit,
            item.Image);

    public static CategorisedAppleItem FromPlatform(ResolvedAppleItem item) =>
        new(
            item.ShowId,
            item.EpisodeId,
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Explicit,
            item.Image);

    public static CategorisedYouTubeItem FromPlatform(ResolvedYouTubeItem item) =>
        new(
            item.ShowId,
            item.EpisodeId,
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration,
            item.Url,
            item.Explicit,
            item.Image,
            item.PlaylistId);
}
