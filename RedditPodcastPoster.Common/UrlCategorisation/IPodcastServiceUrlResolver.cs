namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IPodcastServiceUrlResolver
{
    public bool IsMatch(Uri url);
}