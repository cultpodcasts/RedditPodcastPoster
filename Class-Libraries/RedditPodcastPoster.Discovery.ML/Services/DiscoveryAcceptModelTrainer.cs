using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using RedditPodcastPoster.Discovery.ML.Models;

namespace RedditPodcastPoster.Discovery.ML.Services;

public sealed record DiscoveryAcceptTrainOptions
{
    /// <summary>Share of accepted results the hide threshold must keep visible (chosen on the calibration slice).</summary>
    public float TargetRecall { get; init; } = 0.97f;

    /// <summary>When set, used instead of choosing a threshold from <see cref="TargetRecall"/>.</summary>
    public float? FixedThreshold { get; init; }

    public float PositiveExampleWeight { get; init; } = 15f;

    public float ShowRatePriorWeight { get; init; } = ShowAcceptSmoothing.DefaultPriorWeight;
}

/// <summary>
/// Trains the discovery accept model. Rows are split by time (train 70%, calibration 10%, test 20%) so
/// evaluation always predicts later runs from earlier ones. The LightGBM score is Platt-calibrated on the
/// calibration slice, which is not reweighted, so <c>acceptProbability</c> estimates the real accept rate.
/// </summary>
public sealed class DiscoveryAcceptModelTrainer
{
    public const string ModelFileName = "discovery-accept.model.zip";
    public const string ManifestFileName = "discovery-accept.manifest.json";
    public const string MetricsFileName = "training-metrics.txt";
    public const string ShowAcceptRatesFileName = "show-accept-rates.csv";

    private const double TrainShare = 0.7;
    private const double CalibrationShare = 0.1;

    public async Task<DiscoveryAcceptTrainResult> TrainAsync(
        IReadOnlyList<DiscoveryTrainingExample> examples,
        string outputDirectory,
        DiscoveryAcceptTrainOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DiscoveryAcceptTrainOptions();
        if (examples.Count < 100)
        {
            throw new InvalidOperationException($"Need at least 100 labeled examples; got {examples.Count}.");
        }

        Directory.CreateDirectory(outputDirectory);
        var ordered = examples.OrderBy(x => x.DiscoveryBegan).ToList();
        var trainEnd = (int)(ordered.Count * TrainShare);
        var calibrationEnd = (int)(ordered.Count * (TrainShare + CalibrationShare));
        var trainRows = ordered.Take(trainEnd).ToList();
        var calibrationRows = ordered.Skip(trainEnd).Take(calibrationEnd - trainEnd).ToList();
        var testRows = ordered.Skip(calibrationEnd).ToList();

        var prior = (float)trainRows.Count(x => x.Label) / trainRows.Count;
        var smoothing = new ShowAcceptSmoothing(prior, options.ShowRatePriorWeight, ShowAcceptSmoothing.DefaultSeenCountCap);
        var showFeatures = ShowAcceptHistory.PointInTime(
            ordered.Select(x => (x.ShowName, x.DiscoveryBegan, x.Label)).ToList(),
            smoothing);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].ShowAcceptRate = showFeatures[i].AcceptRate;
            ordered[i].ShowSeenCount = showFeatures[i].SeenCount;
            ordered[i].ExampleWeight = 1f;
        }

        foreach (var row in trainRows.Where(x => x.Label))
        {
            row.ExampleWeight = options.PositiveExampleWeight;
        }

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainRows);
        var calibrationData = mlContext.Data.LoadFromEnumerable(calibrationRows);
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
                nameof(DiscoveryTrainingExample.ShowAcceptRate),
                nameof(DiscoveryTrainingExample.ShowSeenCount))
            .Append(mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: nameof(DiscoveryTrainingExample.Label),
                featureColumnName: "Features",
                exampleWeightColumnName: nameof(DiscoveryTrainingExample.ExampleWeight),
                numberOfLeaves: 63,
                numberOfIterations: 200,
                minimumExampleCountPerLeaf: 50,
                learningRate: 0.05));

        var scorer = pipeline.Fit(trainData);
        var calibrator = mlContext.BinaryClassification.Calibrators
            .Platt(labelColumnName: nameof(DiscoveryTrainingExample.Label), scoreColumnName: "Score")
            .Fit(scorer.Transform(calibrationData));
        var model = scorer.Append(calibrator);

        var calibrationScored = Scored(mlContext, model.Transform(calibrationData));
        var threshold = options.FixedThreshold ?? DiscoveryThresholds.ForTargetRecall(calibrationScored, options.TargetRecall);

        var testPredictions = model.Transform(testData);
        var metrics = mlContext.BinaryClassification.Evaluate(testPredictions, labelColumnName: nameof(DiscoveryTrainingExample.Label));
        var testScored = Scored(mlContext, testPredictions);
        var atThreshold = DiscoveryThresholds.Evaluate(testScored, threshold);
        var unseenShows = testRows
            .Select((row, index) => (row, scored: testScored[index]))
            .Where(x => x.row.ShowSeenCount == 0f)
            .Select(x => x.scored)
            .ToList();

        var modelPath = Path.Combine(outputDirectory, ModelFileName);
        mlContext.Model.Save(model, trainData.Schema, modelPath);

        var showRatesPath = Path.Combine(outputDirectory, ShowAcceptRatesFileName);
        ShowAcceptRateLookup.Write(showRatesPath, ShowAcceptHistory.Totals(ordered.Select(x => (x.ShowName, x.Label))));

        var trainedAt = DateTime.UtcNow;
        var manifest = new DiscoveryAcceptManifest
        {
            ModelVersion = DiscoveryAcceptManifest.CreateModelVersion(trainedAt),
            FeatureVersion = DiscoveryAcceptManifest.CurrentFeatureVersion,
            TrainedAt = trainedAt,
            LabelsFrom = ordered[0].DiscoveryBegan,
            LabelsTo = ordered[^1].DiscoveryBegan,
            TrainingRows = trainRows.Count,
            CalibrationRows = calibrationRows.Count,
            TestRows = testRows.Count,
            EmbeddingDimensions = DiscoveryFeatureBuilder.EmbeddingDimensions,
            AutoHideThreshold = threshold,
            TargetRecall = options.FixedThreshold == null ? options.TargetRecall : null,
            TestPrecisionAtThreshold = atThreshold.Precision,
            TestRecallAtThreshold = atThreshold.Recall,
            ShowRatePrior = smoothing.Prior,
            ShowRatePriorWeight = smoothing.PriorWeight,
            ShowSeenCountCap = smoothing.SeenCountCap
        };

        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        var metricsPath = Path.Combine(outputDirectory, MetricsFileName);
        await File.WriteAllTextAsync(
            metricsPath,
            MetricsReport(manifest, metrics, testScored, atThreshold, unseenShows),
            cancellationToken);

        return new DiscoveryAcceptTrainResult(modelPath, manifestPath, metricsPath, showRatesPath, metrics, manifest);
    }

    private static List<ScoredLabel> Scored(MLContext mlContext, IDataView predictions) =>
        mlContext.Data.CreateEnumerable<ThresholdEvaluationRow>(predictions, reuseRowObject: false)
            .Select(x => new ScoredLabel(x.Probability, x.Label))
            .ToList();

    private static string MetricsReport(
        DiscoveryAcceptManifest manifest,
        CalibratedBinaryClassificationMetrics metrics,
        IReadOnlyCollection<ScoredLabel> testScored,
        ThresholdOutcome atThreshold,
        IReadOnlyCollection<ScoredLabel> unseenShows)
    {
        var testAccepted = testScored.Count(x => x.Accepted);
        var sb = new StringBuilder()
            .AppendLine("Discovery accept/reject model training metrics")
            .AppendLine($"Model version: {manifest.ModelVersion} (feature version {manifest.FeatureVersion})")
            .AppendLine($"Labels from {manifest.LabelsFrom:yyyy-MM-dd} to {manifest.LabelsTo:yyyy-MM-dd}")
            .AppendLine($"Rows: train {manifest.TrainingRows:N0}, calibration {manifest.CalibrationRows:N0}, test {manifest.TestRows:N0} (split by discovery time)")
            .AppendLine($"Train accept rate (show-rate prior): {manifest.ShowRatePrior:P2}")
            .AppendLine($"Test accepted: {testAccepted:N0} of {testScored.Count:N0} ({Ratio(testAccepted, testScored.Count):P2})")
            .AppendLine($"AUC: {metrics.AreaUnderRocCurve:F4}")
            .AppendLine($"PR-AUC: {metrics.AreaUnderPrecisionRecallCurve:F4}")
            .AppendLine($"Log-loss: {metrics.LogLoss:F4}")
            .AppendLine()
            .AppendLine(manifest.TargetRecall is { } target
                ? $"Hide threshold {Format(manifest.AutoHideThreshold)} chosen on calibration slice for target recall {target:P0}"
                : $"Hide threshold {Format(manifest.AutoHideThreshold)} fixed by --threshold")
            .AppendLine($"  Test at threshold: shown {atThreshold.Shown:N0}, hidden {atThreshold.Hidden:N0} ({Ratio(atThreshold.Hidden, testScored.Count):P1}), precision {atThreshold.Precision:P1}, recall {atThreshold.Recall:P1}");

        var unseen = DiscoveryThresholds.Evaluate(unseenShows, manifest.AutoHideThreshold);
        sb.AppendLine($"  Shows with no prior history: {unseenShows.Count:N0} rows, {unseenShows.Count(x => x.Accepted):N0} accepted, recall {unseen.Recall:P1}, precision {unseen.Precision:P1}")
            .AppendLine()
            .AppendLine("Test slice by threshold (shown = probability >= threshold):")
            .AppendLine("  threshold   shown   hidden  precision  recall");
        foreach (var t in DiscoveryThresholds.ReportThresholds.Append(manifest.AutoHideThreshold).Distinct().OrderBy(x => x))
        {
            var o = DiscoveryThresholds.Evaluate(testScored, t);
            sb.AppendLine($"  {Format(t),9} {o.Shown,7:N0} {o.Hidden,8:N0} {o.Precision,10:P1} {o.Recall,7:P1}");
        }

        return sb.ToString();
    }

    private static double Ratio(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

    private static string Format(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

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
    string ShowAcceptRatesPath,
    CalibratedBinaryClassificationMetrics Metrics,
    DiscoveryAcceptManifest Manifest);
