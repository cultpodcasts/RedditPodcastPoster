namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastServiceUrlResolver
{
    public bool IsMatch(Uri url);
}