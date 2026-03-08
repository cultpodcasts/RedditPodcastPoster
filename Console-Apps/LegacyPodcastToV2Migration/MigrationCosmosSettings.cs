namespace LegacyPodcastToV2Migration;

public class MigrationCosmosSettings
{
    public required string Endpoint { get; set; }
    public required string AuthKeyOrResourceToken { get; set; }
    public required string DatabaseId { get; set; }
    public required string Container { get; set; }
    public required string PodcastsContainer { get; set; }
    public required string EpisodesContainer { get; set; }
    public bool? UseGateway { get; set; }
}
