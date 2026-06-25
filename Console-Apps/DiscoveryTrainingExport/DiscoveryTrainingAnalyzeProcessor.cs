using System.Globalization;
using System.Text;

namespace DiscoveryTrainingExport;

public class DiscoveryTrainingAnalyzeProcessor
{
    public void Run(string analysisPath)
    {
        analysisPath = Path.GetFullPath(analysisPath);
        var resultsCsv = Path.Combine(analysisPath, "discovery-results.csv");
        var joinsCsv = Path.Combine(analysisPath, "discovery-episode-joins.csv");

        if (!File.Exists(resultsCsv))
        {
            throw new FileNotFoundException("Run export first or provide folder containing discovery-results.csv.", resultsCsv);
        }

        Console.WriteLine("Analyzing discovery-results.csv...");
        var signalReport = AnalyzeAcceptRejectSignals(resultsCsv);
        File.WriteAllText(Path.Combine(analysisPath, "accept-reject-signals.txt"), signalReport);
        Console.WriteLine("Wrote accept-reject-signals.txt");

        var showRatesPath = Path.Combine(analysisPath, "show-accept-rates.csv");
        WriteShowAcceptRates(resultsCsv, showRatesPath);
        Console.WriteLine($"Wrote {showRatesPath}");

        var ignoreCandidatesPath = Path.Combine(analysisPath, "ignore-term-candidates.csv");
        WriteIgnoreCandidates(resultsCsv, ignoreCandidatesPath);
        Console.WriteLine($"Wrote {ignoreCandidatesPath}");

        if (File.Exists(joinsCsv))
        {
            Console.WriteLine("Analyzing discovery-episode-joins.csv...");
            var subjectDiffPath = Path.Combine(analysisPath, "subject-diff-by-show.csv");
            WriteSubjectDiffByShow(joinsCsv, subjectDiffPath);
            Console.WriteLine($"Wrote {subjectDiffPath}");

            var subjectPatternsPath = Path.Combine(analysisPath, "subject-change-patterns.csv");
            WriteSubjectChangePatterns(joinsCsv, subjectPatternsPath);
            Console.WriteLine($"Wrote {subjectPatternsPath}");
        }
    }

    private static void WriteShowAcceptRates(string resultsCsv, string outputPath)
    {
        var header = CsvParser.ParseHeader(resultsCsv);
        var shows = new Dictionary<string, (int Accepted, int Rejected)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CsvParser.ReadRows(resultsCsv))
        {
            var showName = CsvParser.GetField(row, header, "showName").Trim();
            if (string.IsNullOrWhiteSpace(showName))
            {
                continue;
            }

            if (!shows.TryGetValue(showName, out var counts))
            {
                counts = (0, 0);
            }

            var state = CsvParser.GetField(row, header, "state");
            if (string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase))
            {
                counts.Accepted++;
            }
            else if (string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                counts.Rejected++;
            }

            shows[showName] = counts;
        }

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        writer.WriteLine("showName,total,accepted,rejected,acceptRate");

        foreach (var (showName, counts) in shows.OrderByDescending(x => x.Value.Accepted + x.Value.Rejected))
        {
            var total = counts.Accepted + counts.Rejected;
            if (total == 0)
            {
                continue;
            }

            var rate = 100.0 * counts.Accepted / total;
            writer.WriteLine(string.Join(',',
                Csv(showName),
                total,
                counts.Accepted,
                counts.Rejected,
                rate.ToString("F1", CultureInfo.InvariantCulture)));
        }
    }

    private static void WriteIgnoreCandidates(string resultsCsv, string outputPath)
    {
        var header = CsvParser.ParseHeader(resultsCsv);
        const int minRejections = 5;
        var shows = new Dictionary<string, (int Accepted, int Rejected)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CsvParser.ReadRows(resultsCsv))
        {
            var showName = CsvParser.GetField(row, header, "showName").Trim();
            if (string.IsNullOrWhiteSpace(showName))
            {
                continue;
            }

            if (!shows.TryGetValue(showName, out var counts))
            {
                counts = (0, 0);
            }

            var state = CsvParser.GetField(row, header, "state");
            if (string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase))
            {
                counts.Accepted++;
            }
            else if (string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                counts.Rejected++;
            }

            shows[showName] = counts;
        }

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        writer.WriteLine("showName,rejected,accepted,suggestedAction");

        foreach (var (showName, counts) in shows
                     .Where(x => x.Value.Rejected >= minRejections && x.Value.Accepted == 0)
                     .OrderByDescending(x => x.Value.Rejected))
        {
            writer.WriteLine(string.Join(',',
                Csv(showName),
                counts.Rejected,
                counts.Accepted,
                Csv("block-show")));
        }

        foreach (var (showName, counts) in shows
                     .Where(x => x.Value.Rejected >= minRejections * 2 &&
                                 x.Value.Accepted > 0 &&
                                 x.Value.Accepted * 10 < x.Value.Rejected)
                     .OrderByDescending(x => x.Value.Rejected))
        {
            writer.WriteLine(string.Join(',',
                Csv(showName),
                counts.Rejected,
                counts.Accepted,
                Csv("review-show")));
        }
    }

    private static void WriteSubjectDiffByShow(string joinsCsv, string outputPath)
    {
        var header = CsvParser.ParseHeader(joinsCsv);
        var shows = new Dictionary<string, ShowSubjectStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CsvParser.ReadRows(joinsCsv))
        {
            var showName = CsvParser.GetField(row, header, "showName").Trim();
            if (string.IsNullOrWhiteSpace(showName))
            {
                showName = "(unknown)";
            }

            if (!shows.TryGetValue(showName, out var stats))
            {
                stats = new ShowSubjectStats();
            }

            stats.JoinCount++;
            if (string.Equals(CsvParser.GetField(row, header, "subjectsExactMatch"), "true", StringComparison.OrdinalIgnoreCase))
            {
                stats.ExactMatchCount++;
            }
            else
            {
                stats.DiffCount++;
                foreach (var subject in SplitPipe(CsvParser.GetField(row, header, "subjectsAdded")))
                {
                    stats.Added.Increment(subject);
                }

                foreach (var subject in SplitPipe(CsvParser.GetField(row, header, "subjectsRemoved")))
                {
                    stats.Removed.Increment(subject);
                }
            }

            shows[showName] = stats;
        }

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        writer.WriteLine("showName,joinCount,exactMatchCount,diffCount,topAdded,topRemoved");

        foreach (var (showName, stats) in shows.OrderByDescending(x => x.Value.DiffCount))
        {
            writer.WriteLine(string.Join(',',
                Csv(showName),
                stats.JoinCount,
                stats.ExactMatchCount,
                stats.DiffCount,
                Csv(FormatTop(stats.Added, 5)),
                Csv(FormatTop(stats.Removed, 5))));
        }
    }

    private static void WriteSubjectChangePatterns(string joinsCsv, string outputPath)
    {
        var header = CsvParser.ParseHeader(joinsCsv);
        var patterns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CsvParser.ReadRows(joinsCsv))
        {
            if (string.Equals(CsvParser.GetField(row, header, "subjectsExactMatch"), "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var added = SplitPipe(CsvParser.GetField(row, header, "subjectsAdded"));
            var removed = SplitPipe(CsvParser.GetField(row, header, "subjectsRemoved"));

            if (added.Count == 0 && removed.Count == 0)
            {
                continue;
            }

            var key = $"{FormatList(removed)} => {FormatList(added)}";
            patterns.TryGetValue(key, out var count);
            patterns[key] = count + 1;
        }

        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        writer.WriteLine("pattern,count");

        foreach (var (pattern, count) in patterns.OrderByDescending(x => x.Value).Take(200))
        {
            writer.WriteLine($"{Csv(pattern)},{count}");
        }
    }

    private static string AnalyzeAcceptRejectSignals(string resultsCsv)
    {
        var header = CsvParser.ParseHeader(resultsCsv);
        var rows = new List<LabeledRow>();

        foreach (var row in CsvParser.ReadRows(resultsCsv))
        {
            var state = CsvParser.GetField(row, header, "state");
            if (!string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateTime.TryParse(CsvParser.GetField(row, header, "discoveryBegan"), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var discoveryBegan))
            {
                discoveryBegan = DateTime.MinValue;
            }

            rows.Add(new LabeledRow(
                string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase),
                discoveryBegan,
                !string.IsNullOrWhiteSpace(CsvParser.GetField(row, header, "matchingPodcastIds")),
                CsvParser.GetField(row, header, "showName").Trim(),
                SplitPipe(CsvParser.GetField(row, header, "discoverySubjects")).Count,
                CsvParser.GetField(row, header, "sources"),
                !string.IsNullOrWhiteSpace(CsvParser.GetField(row, header, "spotifyEpisodeId")),
                !string.IsNullOrWhiteSpace(CsvParser.GetField(row, header, "appleEpisodeId")),
                !string.IsNullOrWhiteSpace(CsvParser.GetField(row, header, "youTubeVideoId"))));
        }

        rows.Sort((a, b) => a.DiscoveryBegan.CompareTo(b.DiscoveryBegan));
        var splitIndex = (int)(rows.Count * 0.8);
        var train = rows.Take(splitIndex).ToList();
        var test = rows.Skip(splitIndex).ToList();

        var showStats = train
            .Where(x => !string.IsNullOrWhiteSpace(x.ShowName))
            .GroupBy(x => x.ShowName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (Accepted: g.Count(x => x.Accepted), Total: g.Count()),
                StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("Accept/reject signal analysis");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine($"Labeled rows: {rows.Count:N0} (train: {train.Count:N0}, test: {test.Count:N0})");
        sb.AppendLine();

        AppendRateSection(sb, "Overall", train, test, x => true);
        AppendRateSection(sb, "Has matching podcast ID", train, test, x => x.HasMatchingPodcast);
        AppendRateSection(sb, "No matching podcast ID", train, test, x => !x.HasMatchingPodcast);
        AppendRateSection(sb, "Has discovery subjects", train, test, x => x.SubjectCount > 0);
        AppendRateSection(sb, "No discovery subjects", train, test, x => x.SubjectCount == 0);
        AppendRateSection(sb, "Source contains Taddy", train, test, x => x.Sources.Contains("Taddy", StringComparison.OrdinalIgnoreCase));
        AppendRateSection(sb, "Source contains YouTube", train, test, x => x.Sources.Contains("YouTube", StringComparison.OrdinalIgnoreCase));
        AppendRateSection(sb, "Source contains Spotify", train, test, x => x.Sources.Contains("Spotify", StringComparison.OrdinalIgnoreCase));
        AppendRateSection(sb, "Source contains ListenNotes", train, test, x => x.Sources.Contains("ListenNotes", StringComparison.OrdinalIgnoreCase));

        sb.AppendLine();
        sb.AppendLine("Rule baselines (test set):");
        EvaluateRule(sb, "Predict Accept when matchingPodcastIds present",
            test, x => x.HasMatchingPodcast);
        EvaluateRule(sb, "Predict Reject when show never accepted in train (>=5 appearances)",
            test, x => showStats.TryGetValue(x.ShowName, out var stats) && stats.Total >= 5 && stats.Accepted == 0);
        EvaluateRule(sb, "Predict Accept when show accept rate >= 25% in train (>=4 appearances)",
            test, x => showStats.TryGetValue(x.ShowName, out var stats) && stats.Total >= 4 && stats.Accepted * 4 >= stats.Total);
        EvaluateRule(sb, "Combined: Accept if matchingPodcast OR good-show; Reject if bad-show; else Abstain",
            test,
            x => x.HasMatchingPodcast ||
                 (showStats.TryGetValue(x.ShowName, out var good) && good.Total >= 4 && good.Accepted * 4 >= good.Total),
            x => showStats.TryGetValue(x.ShowName, out var bad) && bad.Total >= 5 && bad.Accepted == 0);

        return sb.ToString();
    }

    private static void AppendRateSection(
        StringBuilder sb,
        string label,
        IReadOnlyList<LabeledRow> train,
        IReadOnlyList<LabeledRow> test,
        Func<LabeledRow, bool> predicate)
    {
        var trainRows = train.Where(predicate).ToList();
        var testRows = test.Where(predicate).ToList();
        if (trainRows.Count == 0)
        {
            return;
        }

        var trainRate = 100.0 * trainRows.Count(x => x.Accepted) / trainRows.Count;
        var testRate = testRows.Count == 0 ? double.NaN : 100.0 * testRows.Count(x => x.Accepted) / testRows.Count;
        sb.AppendLine(
            $"  {label}: train accept {trainRate:F1}% ({trainRows.Count:N0}), test accept {testRate:F1}% ({testRows.Count:N0})");
    }

    private static void EvaluateRule(StringBuilder sb, string label, IReadOnlyList<LabeledRow> test, Func<LabeledRow, bool> predictPositive)
    {
        var predicted = test.Where(predictPositive).ToList();
        var tp = predicted.Count(x => x.Accepted);
        var fp = predicted.Count(x => !x.Accepted);
        var fn = test.Count(x => x.Accepted) - tp;
        var precision = tp + fp == 0 ? 0 : 100.0 * tp / (tp + fp);
        var recall = tp + fn == 0 ? 0 : 100.0 * tp / (tp + fn);
        sb.AppendLine($"  {label}");
        sb.AppendLine($"    predicted positive: {predicted.Count:N0}, precision: {precision:F1}%, recall: {recall:F1}%");
    }

    private static void EvaluateRule(
        StringBuilder sb,
        string label,
        IReadOnlyList<LabeledRow> test,
        Func<LabeledRow, bool> predictAccept,
        Func<LabeledRow, bool> predictReject)
    {
        var acceptPredictions = test.Where(predictAccept).ToList();
        var rejectPredictions = test.Where(predictReject).ToList();
        var abstain = test.Count - acceptPredictions.Count - rejectPredictions.Count;

        var acceptTp = acceptPredictions.Count(x => x.Accepted);
        var rejectTn = rejectPredictions.Count(x => !x.Accepted);

        sb.AppendLine($"  {label}");
        sb.AppendLine($"    auto-accept: {acceptPredictions.Count:N0} (correct {acceptTp:N0}), auto-reject: {rejectPredictions.Count:N0} (correct {rejectTn:N0}), abstain: {abstain:N0}");
    }

    private static List<string> SplitPipe(string value) =>
        value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string FormatList(IEnumerable<string> values) =>
        string.Join(" + ", values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static string FormatTop(Dictionary<string, int> counts, int take) =>
        string.Join("|", counts.OrderByDescending(x => x.Value).Take(take).Select(x => $"{x.Key} ({x.Value})"));

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private sealed record LabeledRow(
        bool Accepted,
        DateTime DiscoveryBegan,
        bool HasMatchingPodcast,
        string ShowName,
        int SubjectCount,
        string Sources,
        bool HasSpotify,
        bool HasApple,
        bool HasYouTube);

    private sealed class ShowSubjectStats
    {
        public int JoinCount { get; set; }
        public int ExactMatchCount { get; set; }
        public int DiffCount { get; set; }
        public Dictionary<string, int> Added { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Removed { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

internal static class CounterExtensions
{
    public static void Increment(this Dictionary<string, int> dictionary, string key)
    {
        dictionary.TryGetValue(key, out var count);
        dictionary[key] = count + 1;
    }
}
