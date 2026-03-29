namespace RedditPodcastPoster.Persistence;

public class CosmosDbSettings
{
    public required string Endpoint { get; set; }
    public required string AuthKeyOrResourceToken { get; set; }
    public required string DatabaseId { get; set; }
    public required string PodcastsContainer { get; set; }
    public required string EpisodesContainer { get; set; }
    public required string SubjectsContainer { get; set; }
    public required string ActivitiesContainer { get; set; }
    public required string DiscoveryContainer { get; set; }
    public required string LookUpsContainer { get; set; }
    public required string PushSubscriptionsContainer { get; set; }
    public bool? UseGateway { get; set; }
}
