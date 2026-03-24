namespace RedditPodcastPoster.Persistence.Legacy;

public class CosmosDbSettings
{
    public required string Endpoint { get; set; }
    public required string AuthKeyOrResourceToken { get; set; }
    public required string DatabaseId { get; set; }
    public required string Container { get; set; }
    public bool? UseGateway { get; set; }
}
