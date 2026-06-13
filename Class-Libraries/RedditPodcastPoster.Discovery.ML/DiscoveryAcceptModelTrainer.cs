using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace RedditPodcastPoster.Discovery.ML;

public sealed class DiscoveryAcceptModelTrainer
{
    public async Task<DiscoveryAcceptTrainResult> TrainAsync(
        IReadOnlyList<DiscoveryTrainingExample> examples,
        string outputDirectory,
        float autoHideThreshold,
        float positiveExampleWeight = 15f,
        CancellationToken cancellationToken = default)
    {
        if (examples.Count < 100)
        {
            throw new InvalidOperationException($"Need at least 100 labeled examples; got {examples.Count}.");
        }

        Directory.CreateDirectory(outputDirectory);
        var ordered = examples
            .OrderBy(x => x.DiscoveryBegan)
            .ToList();
        var splitIndex = (int)(ordered.Count * 0.8);
        var trainRows = ordered.Take(splitIndex).ToList();
        var testRows = ordered.Skip(splitIndex).ToList();

        foreach (var row in trainRows.Where(x => x.Label))
        {
            row.ExampleWeight = positiveExampleWeight;
        }

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainRows);
        var testData = mlContext.Data.LoadFromEnumerable(testRows);

        var pipeline = mlContext.Transforms.Concatenate(
                "Features",
                nameof(DiscoveryTrainingExample.Embedding),
                nameof(DiscoveryTrainingExample.HasMatchingPodcast),
                nameof(DiscoveryTrainingExample.SubjectCount),
                nameof(DiscoveryTrainingExample.SourceListenNotes),
                nameof(DiscoveryTrainingExample.SourceSpotify),
                nameof(DiscoveryTrainingExample.SourceYouTube),
                nameof(DiscoveryTrainingExample.SourceTaddy),
                nameof(DiscoveryTrainingExample.ShowAcceptRate))
            .Append(mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: nameof(DiscoveryTrainingExample.Label),
                featureColumnName: "Features",
                exampleWeightColumnName: nameof(DiscoveryTrainingExample.ExampleWeight),
                numberOfLeaves: 63,
                numberOfIterations: 200,
                minimumExampleCountPerLeaf: 50,
                learningRate: 0.05));

        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(testData);
        var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(DiscoveryTrainingExample.Label));

        var (precision, recall) = EvaluateAtThreshold(predictions, mlContext, autoHideThreshold);

        var modelPath = Path.Combine(outputDirectory, "discovery-accept.model.zip");
        mlContext.Model.Save(model, trainData.Schema, modelPath);

        var manifest = new DiscoveryAcceptManifest
        {
            ModelVersion = "1",
            TrainedAt = DateTime.UtcNow,
            TrainingRows = trainRows.Count,
            EmbeddingDimensions = DiscoveryFeatureBuilder.EmbeddingDimensions,
            AutoHideThreshold = autoHideThreshold,
            TestPrecisionAtThreshold = precision,
            TestRecallAtThreshold = recall
        };

        var manifestPath = Path.Combine(outputDirectory, "discovery-accept.manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        var metricsPath = Path.Combine(outputDirectory, "training-metrics.txt");
        var metricsText = new StringBuilder()
            .AppendLine("Discovery accept/reject model training metrics")
            .AppendLine($"Train rows: {trainRows.Count:N0}")
            .AppendLine($"Test rows: {testRows.Count:N0}")
            .AppendLine($"Accuracy: {metrics.Accuracy:P2}")
            .AppendLine($"AUC: {metrics.AreaUnderRocCurve:F4}")
            .AppendLine($"F1: {metrics.F1Score:F4}")
            .AppendLine($"Precision (default): {metrics.PositivePrecision:F4}")
            .AppendLine($"Recall (default): {metrics.PositiveRecall:F4}")
            .AppendLine($"Precision @ threshold {autoHideThreshold.ToString(CultureInfo.InvariantCulture)}: {precision:F4}")
            .AppendLine($"Recall @ threshold {autoHideThreshold.ToString(CultureInfo.InvariantCulture)}: {recall:F4}")
            .ToString();
        await File.WriteAllTextAsync(metricsPath, metricsText, cancellationToken);

        return new DiscoveryAcceptTrainResult(modelPath, manifestPath, metricsPath, metrics, precision, recall);
    }

    private static (float Precision, float Recall) EvaluateAtThreshold(
        IDataView predictions,
        MLContext mlContext,
        float threshold)
    {
        var scored = mlContext.Data.CreateEnumerable<ThresholdEvaluationRow>(predictions, reuseRowObject: false).ToList();
        var positives = scored.Where(x => x.Probability >= threshold).ToList();
        var tp = positives.Count(x => x.Label);
        var fp = positives.Count(x => !x.Label);
        var fn = scored.Count(x => x.Label) - tp;

        var precision = tp + fp == 0 ? 0f : (float)tp / (tp + fp);
        var recall = tp + fn == 0 ? 0f : (float)tp / (tp + fn);
        return (precision, recall);
    }

    private sealed class ThresholdEvaluationRow
    {
        [ColumnName("Label")]
        public bool Label { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }
    }
}

public sealed record DiscoveryAcceptTrainResult(
    string ModelPath,
    string ManifestPath,
    string MetricsPath,
    BinaryClassificationMetrics Metrics,
    float TestPrecisionAtThreshold,
    float TestRecallAtThreshold);
