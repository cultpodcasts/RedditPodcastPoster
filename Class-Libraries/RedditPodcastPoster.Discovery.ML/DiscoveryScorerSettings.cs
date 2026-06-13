namespace RedditPodcastPoster.Discovery.ML;

public class DiscoveryScorerSettings
{
    public bool Enabled { get; set; }

    /// <summary>Local directory override. When unset, blob settings are used in Azure.</summary>
    public string? ModelDirectory { get; set; }

    public string? BlobStorageAccountName { get; set; }

    public string? BlobContainerName { get; set; }

    /// <summary>Blob prefix folder, e.g. "current" → current/discovery-accept.model.zip</summary>
    public string? BlobPrefix { get; set; }

    /// <summary>Optional user-assigned managed identity client id for blob access.</summary>
    public string? ManagedIdentityClientId { get; set; }

    public string ModelFileName { get; set; } = "discovery-accept.model.zip";

    public string ManifestFileName { get; set; } = "discovery-accept.manifest.json";

    public string OnnxModelFileName { get; set; } = "model.onnx";

    public string TokenizerFileName { get; set; } = "vocab.txt";

    public string ShowAcceptRatesFileName { get; set; } = "show-accept-rates.csv";

    /// <summary>Results with accept probability below this are auto-hidden when enabled.</summary>
    public float AutoHideThreshold { get; set; } = 0.05f;

    public int EmbeddingMaxLength { get; set; } = 256;
}
