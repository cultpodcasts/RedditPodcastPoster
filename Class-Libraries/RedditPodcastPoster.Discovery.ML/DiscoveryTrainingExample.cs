using Microsoft.ML.Data;

namespace RedditPodcastPoster.Discovery.ML;

public sealed class DiscoveryTrainingExample
{
    public DateTime DiscoveryBegan { get; set; }

    [ColumnName("Label")]
    public bool Label { get; set; }

    [VectorType(DiscoveryFeatureBuilder.EmbeddingDimensions)]
    public float[] Embedding { get; set; } = new float[DiscoveryFeatureBuilder.EmbeddingDimensions];

    public float HasMatchingPodcast { get; set; }

    public float SubjectCount { get; set; }

    public float SourceListenNotes { get; set; }

    public float SourceSpotify { get; set; }

    public float SourceYouTube { get; set; }

    public float SourceTaddy { get; set; }

    public float ShowAcceptRate { get; set; }

    public float ExampleWeight { get; set; } = 1f;
}

public sealed class DiscoveryPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}
