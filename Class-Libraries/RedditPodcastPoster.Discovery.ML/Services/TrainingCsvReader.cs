using System.Globalization;

namespace RedditPodcastPoster.Discovery.ML.Services;

public static class TrainingCsvReader
{
    public static IEnumerable<TrainingCsvRow> ReadLabeledRows(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        foreach (var row in ReadLabeledRows(reader))
        {
            yield return row;
        }
    }

    public static IEnumerable<TrainingCsvRow> ReadLabeledRows(TextReader reader)
    {
        Dictionary<string, int>? header = null;
        foreach (var fields in TrainingCsv.ReadRecords(reader))
        {
            if (header == null)
            {
                header = TrainingCsv.ToHeaderIndex(fields);
                continue;
            }

            var state = TrainingCsv.GetField(fields, header, "state");
            if (!string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateTime.TryParse(TrainingCsv.GetField(fields, header, "discoveryBegan"), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var discoveryBegan))
            {
                discoveryBegan = DateTime.MinValue;
            }

            yield return new TrainingCsvRow
            {
                Accepted = string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase),
                DiscoveryBegan = discoveryBegan,
                ShowName = TrainingCsv.GetField(fields, header, "showName"),
                EpisodeName = TrainingCsv.GetField(fields, header, "episodeName"),
                Description = TrainingCsv.GetField(fields, header, "episodeDescription"),
                ShowDescription = TrainingCsv.GetField(fields, header, "showDescription"),
                MatchingPodcastIds = TrainingCsv.GetField(fields, header, "matchingPodcastIds"),
                DiscoverySubjects = TrainingCsv.GetField(fields, header, "discoverySubjects"),
                Sources = TrainingCsv.GetField(fields, header, "sources"),
                AutoHidden = string.Equals(TrainingCsv.GetField(fields, header, "autoHidden"), "true",
                    StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    /// <summary>
    /// A rejected result the scorer auto-hid was never shown to a curator, so its "Rejected" state is not a
    /// curator decision. Training on it teaches the model its own past mistakes.
    /// </summary>
    public static bool IsCuratorLabel(TrainingCsvRow row) => row.Accepted || !row.AutoHidden;
}

public sealed class TrainingCsvRow
{
    public required bool Accepted { get; init; }
    public required DateTime DiscoveryBegan { get; init; }
    public string? ShowName { get; init; }
    public string? EpisodeName { get; init; }
    public string? Description { get; init; }
    public string? ShowDescription { get; init; }
    public string? MatchingPodcastIds { get; init; }
    public string? DiscoverySubjects { get; init; }
    public string? Sources { get; init; }
    public bool AutoHidden { get; init; }
}
