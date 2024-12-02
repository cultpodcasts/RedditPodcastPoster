namespace RedditPodcastPoster.InternetArchive;

public interface IInternetArchiveHttpClientFactory
{
    IInternetArchiveHttpClient Create();
}