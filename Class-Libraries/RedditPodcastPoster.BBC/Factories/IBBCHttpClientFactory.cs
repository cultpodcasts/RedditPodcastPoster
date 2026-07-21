using RedditPodcastPoster.BBC.Clients;

namespace RedditPodcastPoster.BBC.Factories;

public interface IBBCHttpClientFactory
{
    IBBCHttpClient Create();
}
