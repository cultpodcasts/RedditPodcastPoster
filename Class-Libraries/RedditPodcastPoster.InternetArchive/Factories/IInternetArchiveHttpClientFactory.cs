using RedditPodcastPoster.InternetArchive.Clients;

namespace RedditPodcastPoster.InternetArchive.Factories;

public interface IInternetArchiveHttpClientFactory
{
    IInternetArchiveHttpClient Create();
}
