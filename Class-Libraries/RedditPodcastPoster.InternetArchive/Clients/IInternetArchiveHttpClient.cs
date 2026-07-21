namespace RedditPodcastPoster.InternetArchive.Clients;

public interface IInternetArchiveHttpClient
{
    Task<HttpResponseMessage> GetAsync(Uri url);
}
