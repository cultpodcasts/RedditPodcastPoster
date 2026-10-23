using System.Diagnostics;
using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Models.Discovery;

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

        Console.WriteLine($"Loading labeled rows from {csvPath}...");

        var labeled = TrainingCsvReader.ReadLabeledRows(csvPath)
            .OrderBy(x => x.DiscoveryBegan)
            .ToList();
        var rows = request.IncludeHiddenRejects
            ? labeled
            : labeled.Where(TrainingCsvReader.IsCuratorLabel).ToList();
        if (request.MaxRows is > 0)
        {
            rows = rows.Take(request.MaxRows.Value).ToList();
        }

        var withDescription = rows.Count(x => !string.IsNullOrWhiteSpace(x.Description));
        Console.WriteLine(
            $"Loaded {labeled.Count:N0} labeled rows; skipped {labeled.Count - rows.Count:N0} auto-hidden rejects never shown to a curator" +
            (request.IncludeHiddenRejects ? " (none: --include-hidden-rejects)" : string.Empty) + ".");
        if (withDescription == 0)
        {
            Console.WriteLine(
                "WARNING: no row has an episode description. Live scoring embeds descriptions, so this CSV predates the " +
                "description export; re-run DiscoveryTrainingExport.");
        }

        Console.WriteLine($"Training on {rows.Count:N0} rows ({withDescription:N0} with descriptions). Embedding with MiniLM ONNX (this is the slow step)...");
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
            var example = DiscoveryFeatureBuilder.ToExample(embedding, BuildNumericFeatures(row), row.Accepted, row.DiscoveryBegan);
            example.ShowName = row.ShowName;
            examples.Add(example);

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
        var result = await trainer.TrainAsync(examples, outputPath, new DiscoveryAcceptTrainOptions
        {
            TargetRecall = request.TargetRecall,
            FixedThreshold = request.FixedThreshold
        });

        File.Copy(onnxPath, Path.Combine(outputPath, "model.onnx"), overwrite: true);
        File.Copy(vocabPath, Path.Combine(outputPath, "vocab.txt"), overwrite: true);

        Console.WriteLine($"Model: {result.ModelPath}");
        Console.WriteLine($"Show accept rates: {result.ShowAcceptRatesPath}");
        Console.WriteLine($"Metrics: {result.MetricsPath}");
        Console.WriteLine();
        Console.WriteLine(await File.ReadAllTextAsync(result.MetricsPath));
    }

    /// <summary>Show features are filled in by the trainer from point-in-time history.</summary>
    private static DiscoveryNumericFeatures BuildNumericFeatures(TrainingCsvRow row)
    {
        var matchingCount = string.IsNullOrWhiteSpace(row.MatchingPodcastIds)
            ? 0
            : row.MatchingPodcastIds.Split('|', StringSplitOptions.RemoveEmptyEntries).Length;
        var subjects = string.IsNullOrWhiteSpace(row.DiscoverySubjects)
            ? []
            : row.DiscoverySubjects.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sources = ParseSources(row.Sources ?? string.Empty);

        return DiscoveryFeatureBuilder.BuildNumericFeatures(matchingCount, subjects, sources, default);
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
