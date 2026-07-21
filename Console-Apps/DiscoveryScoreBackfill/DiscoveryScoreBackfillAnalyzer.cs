using System.Globalization;
using System.Text;
using System.Text.Json;
using RedditPodcastPoster.Models.Discovery;

namespace DiscoveryScoreBackfill;

public static class DiscoveryScoreBackfillAnalyzer
{
    public static string BuildEvidenceReport(
        IReadOnlyList<DiscoveryResultsDocument> documents,
        IReadOnlyList<ScoredDiscoveryResult> scoredResults,
        bool dryRun,
        float autoHideThreshold,
        string? manifestPath)
    {
        var sb = new StringBuilder();
        var runAt = DateTime.UtcNow;

        sb.AppendLine("# Discovery scorer backfill evidence");
        sb.AppendLine();
        sb.AppendLine($"Generated: {runAt:O}");
        sb.AppendLine($"Mode: {(dryRun ? "dry-run (not saved)" : "live backfill")}");
        sb.AppendLine($"Auto-hide threshold: {autoHideThreshold.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine();

        AppendModelBaseline(sb, manifestPath, autoHideThreshold);
        AppendDocumentsSection(sb, documents);
        AppendPerDocumentStats(sb, documents);
        AppendProbabilityDistribution(sb, scoredResults);
        AppendTopExamples(sb, scoredResults);
        AppendAutoHiddenSamples(sb, scoredResults);
        AppendMatchingPodcastSignal(sb, scoredResults);

        return sb.ToString();
    }

    private static void AppendModelBaseline(StringBuilder sb, string? manifestPath, float autoHideThreshold)
    {
        sb.AppendLine("## Training baseline");
        if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
        {
            var manifest = JsonSerializer.Deserialize<ManifestSummary>(File.ReadAllText(manifestPath));
            if (manifest != null)
            {
                sb.AppendLine($"- Trained at: {manifest.TrainedAt:O}");
                sb.AppendLine($"- Training rows: {manifest.TrainingRows:N0}");
                sb.AppendLine(
                    $"- Test precision @ threshold {autoHideThreshold.ToString(CultureInfo.InvariantCulture)}: {manifest.TestPrecisionAtThreshold:P2}");
                sb.AppendLine(
                    $"- Test recall @ threshold {autoHideThreshold.ToString(CultureInfo.InvariantCulture)}: {manifest.TestRecallAtThreshold:P2}");
                sb.AppendLine(
                    "- Interpretation: at this threshold the model hides ~97% of training rejects while ~52% of hidden items were true rejects (precision on predicted negatives / hidden bucket).");
                sb.AppendLine();
                return;
            }
        }

        sb.AppendLine("- Model manifest not found locally; training baseline ~52% precision @ 0.05 threshold (see discovery-accept.manifest.json).");
        sb.AppendLine();
    }

    private static void AppendDocumentsSection(StringBuilder sb, IReadOnlyList<DiscoveryResultsDocument> documents)
    {
        sb.AppendLine("## Documents");
        sb.AppendLine();
        sb.AppendLine("| Document id | discoveryBegan (UTC) | state | result count |");
        sb.AppendLine("|-------------|------------------------|-------|--------------|");
        foreach (var document in documents.OrderBy(x => x.DiscoveryBegan))
        {
            var count = document.DiscoveryResults?.Count() ?? 0;
            sb.AppendLine(
                $"| `{document.Id}` | {document.DiscoveryBegan:O} | {document.State} | {count:N0} |");
        }

        sb.AppendLine();
    }

    private static void AppendPerDocumentStats(StringBuilder sb, IReadOnlyList<DiscoveryResultsDocument> documents)
    {
        sb.AppendLine("## Per-document summary");
        sb.AppendLine();

        foreach (var document in documents.OrderBy(x => x.DiscoveryBegan))
        {
            var results = document.DiscoveryResults?.ToList() ?? [];
            var total = results.Count;
            var hidden = results.Count(x => x.AutoHidden);
            var visible = total - hidden;
            var pctHidden = total == 0 ? 0 : 100.0 * hidden / total;

            sb.AppendLine($"### `{document.Id}` ({document.DiscoveryBegan:O})");
            sb.AppendLine($"- Total results: {total:N0}");
            sb.AppendLine($"- Auto-hidden: {hidden:N0}");
            sb.AppendLine($"- Visible: {visible:N0}");
            sb.AppendLine($"- % hidden: {pctHidden:F1}%");
            sb.AppendLine();
        }
    }

    private static void AppendProbabilityDistribution(StringBuilder sb, IReadOnlyList<ScoredDiscoveryResult> scoredResults)
    {
        sb.AppendLine("## acceptProbability distribution (all documents)");
        sb.AppendLine();

        var buckets = new (string Label, Func<float, bool> Predicate)[]
        {
            ("0 – 0.05", p => p < 0.05f),
            ("0.05 – 0.2", p => p >= 0.05f && p < 0.2f),
            ("0.2 – 0.5", p => p >= 0.2f && p < 0.5f),
            ("0.5+", p => p >= 0.5f)
        };

        var probabilities = scoredResults
            .Select(x => x.Result.AcceptProbability ?? 0f)
            .ToList();

        sb.AppendLine("| Bucket | Count | % of total |");
        sb.AppendLine("|--------|------:|-----------:|");
        foreach (var (label, predicate) in buckets)
        {
            var count = probabilities.Count(predicate);
            var pct = probabilities.Count == 0 ? 0 : 100.0 * count / probabilities.Count;
            sb.AppendLine($"| {label} | {count:N0} | {pct:F1}% |");
        }

        sb.AppendLine();
    }

    private static void AppendTopExamples(StringBuilder sb, IReadOnlyList<ScoredDiscoveryResult> scoredResults)
    {
        sb.AppendLine("## Top 10 highest acceptProbability");
        sb.AppendLine();
        sb.AppendLine("| showName | episodeName | acceptProbability | matchingPodcast |");
        sb.AppendLine("|----------|-------------|------------------:|-----------------|");

        foreach (var item in scoredResults
                     .OrderByDescending(x => x.Result.AcceptProbability ?? 0f)
                     .ThenBy(x => x.Result.ShowName)
                     .Take(10))
        {
            sb.AppendLine(
                $"| {Escape(item.Result.ShowName)} | {Escape(item.Result.EpisodeName)} | {FormatProb(item.Result.AcceptProbability)} | {FormatMatching(item.Result)} |");
        }

        sb.AppendLine();
    }

    private static void AppendAutoHiddenSamples(StringBuilder sb, IReadOnlyList<ScoredDiscoveryResult> scoredResults)
    {
        sb.AppendLine("## Sample auto-hidden results (up to 10)");
        sb.AppendLine();
        sb.AppendLine("| showName | episodeName | acceptProbability | matchingPodcast |");
        sb.AppendLine("|----------|-------------|------------------:|-----------------|");

        foreach (var item in scoredResults
                     .Where(x => x.Result.AutoHidden)
                     .OrderBy(x => x.Result.AcceptProbability ?? 0f)
                     .ThenBy(x => x.Result.ShowName)
                     .Take(10))
        {
            sb.AppendLine(
                $"| {Escape(item.Result.ShowName)} | {Escape(item.Result.EpisodeName)} | {FormatProb(item.Result.AcceptProbability)} | {FormatMatching(item.Result)} |");
        }

        sb.AppendLine();
    }

    private static void AppendMatchingPodcastSignal(StringBuilder sb, IReadOnlyList<ScoredDiscoveryResult> scoredResults)
    {
        sb.AppendLine("## matchingPodcastIds signal (visible vs hidden)");
        sb.AppendLine();
        sb.AppendLine("| Visibility | Total | With matchingPodcast | Without | % with matching | Avg acceptProbability |");
        sb.AppendLine("|------------|------:|---------------------:|--------:|----------------:|----------------------:|");

        foreach (var group in new[] { ("Visible", false), ("Hidden", true) })
        {
            var rows = scoredResults
                .Where(x => x.Result.AutoHidden == group.Item2)
                .Select(x => x.Result)
                .ToList();
            AppendMatchingRow(sb, group.Item1, rows);
        }

        sb.AppendLine();
        sb.AppendLine("Training context: accepted discovery results disproportionately have matchingPodcastIds; the scorer should hide few rows with matches and many without.");
        sb.AppendLine();
    }

    private static void AppendMatchingRow(StringBuilder sb, string label, IReadOnlyList<DiscoveryResult> rows)
    {
        var total = rows.Count;
        var withMatching = rows.Count(x => x.MatchingPodcastIds.Length > 0);
        var without = total - withMatching;
        var pctWithMatching = total == 0 ? 0 : 100.0 * withMatching / total;
        var avgProb = total == 0 ? 0 : rows.Average(x => x.AcceptProbability ?? 0f);

        sb.AppendLine(
            $"| {label} | {total:N0} | {withMatching:N0} | {without:N0} | {pctWithMatching:F1}% | {avgProb:F4} |");
    }

    private static string Escape(string? value) =>
        (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal);

    private static string FormatProb(float? value) =>
        value?.ToString("F4", CultureInfo.InvariantCulture) ?? "n/a";

    private static string FormatMatching(DiscoveryResult result) =>
        result.MatchingPodcastIds.Length > 0 ? "yes" : "no";

    private sealed class ManifestSummary
    {
        public DateTime TrainedAt { get; set; }
        public int TrainingRows { get; set; }
        public float TestPrecisionAtThreshold { get; set; }
        public float TestRecallAtThreshold { get; set; }
    }
}
