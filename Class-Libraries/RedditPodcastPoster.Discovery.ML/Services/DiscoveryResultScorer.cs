using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Discovery.ML.Configuration;
using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.ML.Services;

public sealed class DiscoveryResultScorer : IDiscoveryResultScorer, IDisposable
{
    private readonly DiscoveryScorerSettings _settings;
    private readonly ILogger<DiscoveryResultScorer> _logger;
    private readonly Lazy<ScorerResources?> _resources;

    public DiscoveryResultScorer(IOptions<DiscoveryScorerSettings> settings, ILogger<DiscoveryResultScorer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _resources = new Lazy<ScorerResources?>(TryLoadResources);
    }

    public bool IsEnabled => _settings.Enabled && _resources.Value != null;

    public DiscoveryScoreResult Score(DiscoveryResult discoveryResult)
    {
        var resources = _resources.Value;
        if (resources == null)
        {
            return new DiscoveryScoreResult(1f, false);
        }

        try
        {
            var text = DiscoveryFeatureBuilder.BuildEmbeddingText(discoveryResult);
            var embedding = resources.Embedder.Embed(text);
            var numeric = DiscoveryFeatureBuilder.BuildNumericFeatures(discoveryResult, resources.ShowAcceptRates);
            var example = new DiscoveryTrainingExample
            {
                Label = false,
                Embedding = embedding,
                HasMatchingPodcast = numeric.HasMatchingPodcast,
                SubjectCount = numeric.SubjectCount,
                SourceListenNotes = numeric.SourceListenNotes,
                SourceSpotify = numeric.SourceSpotify,
                SourceYouTube = numeric.SourceYouTube,
                SourceTaddy = numeric.SourceTaddy,
                ShowAcceptRate = numeric.ShowAcceptRate
            };

            var probability = resources.Predictor.PredictAcceptProbability(example);
            var shouldHide = probability < _settings.AutoHideThreshold;
            return new DiscoveryScoreResult(probability, shouldHide);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery scorer failed for result '{ResultId}'. Allowing result through.", discoveryResult.Id);
            return new DiscoveryScoreResult(1f, false);
        }
    }

    public void Dispose()
    {
        if (_resources.IsValueCreated)
        {
            _resources.Value?.Dispose();
        }
    }

    private ScorerResources? TryLoadResources()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Discovery scorer disabled in configuration.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_settings.ModelDirectory) &&
            string.IsNullOrWhiteSpace(_settings.BlobContainerName))
        {
            _logger.LogWarning(
                "Discovery scorer enabled but neither {ModelDirectory} nor {BlobContainer} is configured.",
                nameof(DiscoveryScorerSettings.ModelDirectory),
                nameof(DiscoveryScorerSettings.BlobContainerName));
            return null;
        }

        var directory = DiscoveryModelBlobSync.ResolveModelDirectory(_settings, _logger);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        directory = Path.GetFullPath(directory);
        var modelPath = Path.Combine(directory, _settings.ModelFileName);
        var manifestPath = Path.Combine(directory, _settings.ManifestFileName);
        var onnxPath = Path.Combine(directory, _settings.OnnxModelFileName);
        var tokenizerPath = Path.Combine(directory, _settings.TokenizerFileName);
        var showRatesPath = Path.Combine(directory, _settings.ShowAcceptRatesFileName);

        if (!File.Exists(modelPath) || !File.Exists(onnxPath) || !File.Exists(tokenizerPath))
        {
            _logger.LogWarning(
                "Discovery scorer files missing under '{Directory}'. Expected model, ONNX, and tokenizer.",
                directory);
            return null;
        }

        if (File.Exists(manifestPath))
        {
            var manifest = JsonSerializer.Deserialize<DiscoveryAcceptManifest>(File.ReadAllText(manifestPath));
            if (manifest != null)
            {
                _logger.LogInformation(
                    "Loaded discovery accept model trained at {TrainedAt:O} on {Rows:N0} rows (test precision @ threshold: {Precision:P1}).",
                    manifest.TrainedAt, manifest.TrainingRows, manifest.TestPrecisionAtThreshold);
            }
        }

        var showRates = ShowAcceptRateLookup.Load(showRatesPath);
        var embedder = new MiniLmOnnxEmbedder(onnxPath, tokenizerPath, _settings.EmbeddingMaxLength);
        var predictor = new DiscoveryAcceptModelPredictor(modelPath);
        _logger.LogInformation("Discovery scorer enabled. Auto-hide threshold: {Threshold}.", _settings.AutoHideThreshold);
        return new ScorerResources(embedder, predictor, showRates);
    }

    private sealed class ScorerResources(MiniLmOnnxEmbedder embedder, DiscoveryAcceptModelPredictor predictor, IReadOnlyDictionary<string, float> showAcceptRates)
        : IDisposable
    {
        public MiniLmOnnxEmbedder Embedder { get; } = embedder;
        public DiscoveryAcceptModelPredictor Predictor { get; } = predictor;
        public IReadOnlyDictionary<string, float> ShowAcceptRates { get; } = showAcceptRates;

        public void Dispose()
        {
            Embedder.Dispose();
            Predictor.Dispose();
        }
    }
}

public sealed class DisabledDiscoveryResultScorer : IDiscoveryResultScorer
{
    public bool IsEnabled => false;

    public DiscoveryScoreResult Score(DiscoveryResult discoveryResult) => new(1f, false);
}
