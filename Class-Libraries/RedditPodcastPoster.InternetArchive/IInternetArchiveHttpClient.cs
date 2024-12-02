namespace RedditPodcastPoster.InternetArchive;

public interface IInternetArchiveHttpClient
{
    Task<HttpResponseMessage> GetAsync(Uri url);
}