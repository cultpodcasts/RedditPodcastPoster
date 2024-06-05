namespace RedditPodcastPoster.PodcastServices.Spotify;

public static class SpotifyPodcastServiceMatcher
{
    public static bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("spotify");
    }
}