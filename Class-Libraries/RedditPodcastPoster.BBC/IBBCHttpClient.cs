namespace RedditPodcastPoster.BBC
{
    public interface IBBCHttpClient
    {
        Task<HttpResponseMessage> GetAsync(Uri url);
    }
}

