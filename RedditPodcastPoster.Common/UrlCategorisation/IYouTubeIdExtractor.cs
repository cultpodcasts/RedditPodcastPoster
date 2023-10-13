namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IYouTubeIdExtractor
{
    public string? Extract(Uri youTubeUrl);
}