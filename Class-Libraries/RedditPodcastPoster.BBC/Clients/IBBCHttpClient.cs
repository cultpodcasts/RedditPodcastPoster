namespace RedditPodcastPoster.BBC.Clients;

public interface IBBCHttpClient
{
    Task<HttpResponseMessage> GetAsync(Uri url);
}
