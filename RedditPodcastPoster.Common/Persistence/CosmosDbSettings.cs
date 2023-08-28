namespace RedditPodcastPoster.Common.Persistence;

public class CosmosDbSettings
{
    public string Endpoint { get; set; } = "";
    public string AuthKeyOrResourceToken { get; set; } = "";
    public string DatabaseId { get; set; } = "";
    public string Container { get; set; } = "";
}