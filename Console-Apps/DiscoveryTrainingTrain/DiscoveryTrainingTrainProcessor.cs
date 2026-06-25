using System.Diagnostics;
using RedditPodcastPoster.Discovery.ML;
using RedditPodcastPoster.Models;

namespace DiscoveryTrainingTrain;

public class DiscoveryTrainingTrainProcessor
{
    private const int ProgressRowInterval = 100;
    private static readonly TimeSpan ProgressTimeInterval = TimeSpan.FromSeconds(15);
    private static int _lastProgressLength;

    public async Task RunAsync(DiscoveryTrainingTrainRequest request)
    {
        var csvPath = Path.GetFullPath(request.CsvPath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var onnxDirectory = Path.GetFullPath(request.OnnxModelDirectory ?? Path.Combine(outputPath, "onnx"));

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("Training CSV not found.", csvPath);
        }

        Directory.CreateDirectory(outputPath);

        if (!request.SkipDownload)
        {
            await OnnxModelDownloader.EnsureMiniLmModelAsync(onnxDirectory);
        }

        var showRatesPath = request.ShowAcceptRatesPath != null
            ? Path.GetFullPath(request.ShowAcceptRatesPath)
            : Path.Combine(Path.GetDirectoryName(csvPath) ?? outputPath, "show-accept-rates.csv");
        var showRates = ShowAcceptRateLookup.Load(showRatesPath);

        Console.WriteLine($"Loading labeled rows from {csvPath}...");

        var rows = TrainingCsvReader.ReadLabeledRows(csvPath)
            .OrderBy(x => x.DiscoveryBegan)
            .ToList();
        if (request.MaxRows is > 0)
        {
            rows = rows.Take(request.MaxRows.Value).ToList();
        }

        Console.WriteLine($"Loaded {rows.Count:N0} rows. Embedding with MiniLM ONNX (this is the slow step)...");
        var onnxPath = Path.Combine(onnxDirectory, "model.onnx");
        var vocabPath = Path.Combine(onnxDirectory, "vocab.txt");
        using var embedder = new MiniLmOnnxEmbedder(onnxPath, vocabPath);

        var examples = new List<DiscoveryTrainingExample>(rows.Count);
        var stopwatch = Stopwatch.StartNew();
        var lastProgressAt = stopwatch.Elapsed;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var text = DiscoveryFeatureBuilder.BuildEmbeddingText(
                row.ShowName, row.EpisodeName, row.Description, row.ShowDescription);
            var embedding = embedder.Embed(text);
            var numeric = BuildNumericFeatures(row, showRates);
            examples.Add(new DiscoveryTrainingExample
            {
                DiscoveryBegan = row.DiscoveryBegan,
                Label = row.Accepted,
                Embedding = embedding,
                HasMatchingPodcast = numeric.HasMatchingPodcast,
                SubjectCount = numeric.SubjectCount,
                SourceListenNotes = numeric.SourceListenNotes,
                SourceSpotify = numeric.SourceSpotify,
                SourceYouTube = numeric.SourceYouTube,
                SourceTaddy = numeric.SourceTaddy,
                ShowAcceptRate = numeric.ShowAcceptRate
            });

            var completed = i + 1;
            var dueByCount = completed % ProgressRowInterval == 0;
            var dueByTime = stopwatch.Elapsed - lastProgressAt >= ProgressTimeInterval;
            if (dueByCount || dueByTime || completed == rows.Count)
            {
                WriteEmbeddingProgress(completed, rows.Count, stopwatch.Elapsed);
                lastProgressAt = stopwatch.Elapsed;
            }
        }

        stopwatch.Stop();
        ClearProgressLine();
        Console.WriteLine($"Finished embedding {rows.Count:N0} rows in {FormatDuration(stopwatch.Elapsed)}.");

        Console.WriteLine("Training LightGBM model...");
        var trainer = new DiscoveryAcceptModelTrainer();
        var result = await trainer.TrainAsync(examples, outputPath, request.AutoHideThreshold);

        File.Copy(onnxPath, Path.Combine(outputPath, "model.onnx"), overwrite: true);
        File.Copy(vocabPath, Path.Combine(outputPath, "vocab.txt"), overwrite: true);
        if (File.Exists(showRatesPath))
        {
            File.Copy(showRatesPath, Path.Combine(outputPath, "show-accept-rates.csv"), overwrite: true);
        }

        Console.WriteLine($"Model: {result.ModelPath}");
        Console.WriteLine($"Metrics: {result.MetricsPath}");
        Console.WriteLine($"Test AUC: {result.Metrics.AreaUnderRocCurve:F4}");
        Console.WriteLine($"Test precision @ {request.AutoHideThreshold}: {result.TestPrecisionAtThreshold:P2}");
        Console.WriteLine($"Test recall @ {request.AutoHideThreshold}: {result.TestRecallAtThreshold:P2}");
    }

    private static DiscoveryNumericFeatures BuildNumericFeatures(
        TrainingCsvRow row,
        IReadOnlyDictionary<string, float> showRates)
    {
        var matchingCount = string.IsNullOrWhiteSpace(row.MatchingPodcastIds)
            ? 0
            : row.MatchingPodcastIds.Split('|', StringSplitOptions.RemoveEmptyEntries).Length;
        var subjects = string.IsNullOrWhiteSpace(row.DiscoverySubjects)
            ? []
            : row.DiscoverySubjects.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sources = ParseSources(row.Sources ?? string.Empty);

        return DiscoveryFeatureBuilder.BuildNumericFeatures(
            matchingCount,
            subjects,
            sources,
            row.ShowName,
            showRates);
    }

    private static DiscoverService[] ParseSources(string sources)
    {
        if (string.IsNullOrWhiteSpace(sources))
        {
            return [];
        }

        return sources
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Enum.TryParse<DiscoverService>(x, true, out var service) ? service : (DiscoverService?)null)
            .Where(x => x != null)
            .Select(x => x!.Value)
            .ToArray();
    }

    private static void WriteEmbeddingProgress(int completed, int total, TimeSpan elapsed)
    {
        var percent = 100d * completed / total;
        var rowsPerSecond = completed / Math.Max(elapsed.TotalSeconds, 0.001);
        var remaining = total - completed;
        var eta = rowsPerSecond > 0
            ? TimeSpan.FromSeconds(remaining / rowsPerSecond)
            : TimeSpan.Zero;

        var message =
            $"  Embedding: {completed:N0}/{total:N0} ({percent:F1}%) | {rowsPerSecond:F1} rows/s | elapsed {FormatDuration(elapsed)} | ETA {FormatDuration(eta)}";
        var padding = Math.Max(0, _lastProgressLength - message.Length);
        Console.Write($"\r{message}{new string(' ', padding)}");
        _lastProgressLength = message.Length;
        Console.Out.Flush();
    }

    private static void ClearProgressLine()
    {
        if (_lastProgressLength <= 0)
        {
            return;
        }

        Console.Write($"\r{new string(' ', _lastProgressLength)}\r");
        _lastProgressLength = 0;
        Console.Out.Flush();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.Minutes}m {duration.Seconds:D2}s";
        }

        return $"{duration.Seconds}s";
    }
}
