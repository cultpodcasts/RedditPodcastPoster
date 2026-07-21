using System.Globalization;

namespace RedditPodcastPoster.Discovery.ML.Services;

public static class ShowAcceptRateLookup
{
    public static IReadOnlyDictionary<string, float> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }

        var rates = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(path);
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            var parts = ParseCsvLine(line);
            if (parts.Length < 5)
            {
                continue;
            }

            var showName = parts[0].Trim('"');
            if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var acceptRate))
            {
                continue;
            }

            rates[showName] = acceptRate / 100f;
        }

        return rates;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
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
