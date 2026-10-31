using System.Globalization;
using System.Text;

namespace RedditPodcastPoster.Discovery.ML.Services;

/// <summary><c>show-accept-rates.csv</c>: <c>showName,total,accepted,rejected,acceptRate</c> (rate in percent).</summary>
public static class ShowAcceptRateLookup
{
    public static IReadOnlyDictionary<string, ShowAcceptCounts> Load(string path)
    {
        var counts = new Dictionary<string, ShowAcceptCounts>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return counts;
        }

        foreach (var parts in TrainingCsv.ReadRows(path))
        {
            if (parts.Length < 5)
            {
                continue;
            }

            var key = ShowAcceptHistory.Key(parts[0]);
            if (key == null ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var accepted))
            {
                continue;
            }

            counts[key] = new ShowAcceptCounts(accepted, total);
        }

        return counts;
    }

    public static void Write(string path, IReadOnlyDictionary<string, ShowAcceptCounts> counts)
    {
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.WriteLine("showName,total,accepted,rejected,acceptRate");
        foreach (var (showName, c) in counts.Where(x => x.Value.Total > 0).OrderByDescending(x => x.Value.Total))
        {
            var rate = 100.0 * c.Accepted / c.Total;
            writer.WriteLine(string.Join(',',
                TrainingCsv.Escape(showName),
                c.Total.ToString(CultureInfo.InvariantCulture),
                c.Accepted.ToString(CultureInfo.InvariantCulture),
                (c.Total - c.Accepted).ToString(CultureInfo.InvariantCulture),
                rate.ToString("F1", CultureInfo.InvariantCulture)));
        }
    }
}
