namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

public interface INonPodcastServiceAdapterResolver
{
    INonPodcastServiceAdapter? ForSubmit(Uri url);

    INonPodcastServiceAdapter? ForExtract(Uri url);
}
