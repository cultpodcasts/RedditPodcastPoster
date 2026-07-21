using System.Text;

namespace RedditPodcastPoster.Discovery.ML.Services;

public static class TrainingCsvReader
{
    public static IEnumerable<TrainingCsvRow> ReadLabeledRows(string csvPath)
    {
        var header = CsvParser.ParseHeader(csvPath);
        foreach (var fields in CsvParser.ReadRows(csvPath))
        {
            var state = CsvParser.GetField(fields, header, "state");
            if (!string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateTime.TryParse(CsvParser.GetField(fields, header, "discoveryBegan"), out var discoveryBegan))
            {
                discoveryBegan = DateTime.MinValue;
            }

            yield return new TrainingCsvRow
            {
                Accepted = string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase),
                DiscoveryBegan = discoveryBegan,
                ShowName = CsvParser.GetField(fields, header, "showName"),
                EpisodeName = CsvParser.GetField(fields, header, "episodeName"),
                Description = CsvParser.GetField(fields, header, "episodeDescription"),
                ShowDescription = null,
                MatchingPodcastIds = CsvParser.GetField(fields, header, "matchingPodcastIds"),
                DiscoverySubjects = CsvParser.GetField(fields, header, "discoverySubjects"),
                Sources = CsvParser.GetField(fields, header, "sources")
            };
        }
    }

    public static IReadOnlyDictionary<string, float> LoadShowAcceptRates(string path)
    {
        return ShowAcceptRateLookup.Load(path);
    }
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
}

internal static class CsvParser
{
    public static IEnumerable<string[]> ReadRows(string path)
    {
        using var reader = new StreamReader(path);
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            yield return ParseLine(line);
        }
    }

    public static Dictionary<string, int> ParseHeader(string path)
    {
        using var reader = new StreamReader(path);
        var header = reader.ReadLine()
                     ?? throw new InvalidOperationException($"Empty CSV: '{path}'.");
        return header
            .Split(',')
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetField(string[] fields, IReadOnlyDictionary<string, int> header, string name)
    {
        return header.TryGetValue(name, out var index) && index < fields.Length
            ? fields[index]
            : string.Empty;
    }

    private static string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
