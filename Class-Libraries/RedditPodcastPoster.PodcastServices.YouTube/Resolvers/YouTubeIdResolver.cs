namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

/// <summary>
/// Public YouTube video-id API used across submit, discovery, Bluesky, migrate, and Api.
/// Implementation lives in <see cref="RedditPodcastPoster.Models.YouTube.YouTubeIdResolver"/> so
/// Models catalog accessors share one parser without a Models→YouTube project reference.
/// </summary>
public static class YouTubeIdResolver
{
    public static string? Extract(Uri youTubeUrl) =>
        RedditPodcastPoster.Models.YouTube.YouTubeIdResolver.Extract(youTubeUrl);
}
