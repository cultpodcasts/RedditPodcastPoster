namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IPodcastServiceUrlResolver
{
    public bool IsMatch(Uri url);
}