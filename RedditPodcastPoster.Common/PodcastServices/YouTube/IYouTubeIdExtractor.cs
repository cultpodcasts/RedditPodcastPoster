namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeIdExtractor
{
    public string? Extract(Uri youTubeUrl);
}