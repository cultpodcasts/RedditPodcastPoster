namespace RedditPodcastPoster.Discovery.ML.Configuration;

public class DiscoveryScorerSettings
{
    public bool Enabled { get; set; }

    /// <summary>Local directory override. When unset, blob settings are used in Azure.</summary>
    public string? ModelDirectory { get; set; }

    public string? BlobStorageAccountName { get; set; }

    public string? BlobContainerName { get; set; }

    /// <summary>Blob prefix folder, e.g. "current" ΓåÆ current/discovery-accept.model.zip</summary>
    public string? BlobPrefix { get; set; }

    /// <summary>Optional user-assigned managed identity client id for blob access.</summary>
    public string? ManagedIdentityClientId { get; set; }

    public string ModelFileName { get; set; } = "discovery-accept.model.zip";

    public string ManifestFileName { get; set; } = "discovery-accept.manifest.json";

    public string OnnxModelFileName { get; set; } = "model.onnx";

    public string TokenizerFileName { get; set; } = "vocab.txt";

    public string ShowAcceptRatesFileName { get; set; } = "show-accept-rates.csv";

    /// <summary>
    /// Results with accept probability below this are auto-hidden when enabled. Used for feature-version-1
    /// models, or for any model when <see cref="UseManifestThreshold"/> is false.
    /// </summary>
    public float AutoHideThreshold { get; set; } = 0.05f;

    /// <summary>When true (default), a feature-version-2+ model's manifest threshold overrides <see cref="AutoHideThreshold"/>.</summary>
    public bool UseManifestThreshold { get; set; } = true;

    public int EmbeddingMaxLength { get; set; } = 256;
}
