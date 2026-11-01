using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// The form a YouTube, Spotify or Apple URL is stored in on an episode, and so on a film trailer or a playable's
/// service link: <c>youtube.com/watch?v={id}</c>, the cleaned Spotify episode URL, or the cleaned Apple URL. A pasted
/// <c>youtu.be</c> or tracking-tagged link maps to the same value. Other keys return the URL unchanged.
/// </summary>
public static class PodcastServiceUrlNormaliser
{
    public static Uri Stored(Uri url, string serviceKey)
    {
        if (serviceKey == ServiceKeys.YouTube)
        {
            var videoId = YouTubeIdResolver.Extract(url);
            return string.IsNullOrWhiteSpace(videoId)
                ? url
                : new Uri($"https://www.youtube.com/watch?v={videoId}");
        }

        if (serviceKey == ServiceKeys.Spotify)
        {
            return string.IsNullOrWhiteSpace(SpotifyIdResolver.GetEpisodeId(url)) ? url : url.CleanSpotifyUrl();
        }

        if (serviceKey == ServiceKeys.Apple)
        {
            return url.CleanAppleUrl();
        }

        return url;
    }
}
