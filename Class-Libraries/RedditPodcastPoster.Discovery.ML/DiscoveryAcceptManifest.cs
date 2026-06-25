namespace RedditPodcastPoster.Discovery.ML;

public sealed class DiscoveryAcceptManifest
{
    public required string ModelVersion { get; set; }

    public required DateTime TrainedAt { get; set; }

    public required int TrainingRows { get; set; }

    public required int EmbeddingDimensions { get; set; }

    public required float AutoHideThreshold { get; set; }

    public required float TestPrecisionAtThreshold { get; set; }

    public required float TestRecallAtThreshold { get; set; }
}
