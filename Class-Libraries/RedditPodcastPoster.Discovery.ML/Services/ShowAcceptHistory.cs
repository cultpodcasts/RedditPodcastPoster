namespace RedditPodcastPoster.Discovery.ML.Services;

public readonly record struct ShowAcceptCounts(int Accepted, int Total);

public readonly record struct ShowAcceptFeatures(float AcceptRate, float SeenCount);

/// <summary>
/// Show-level features. The rate is shrunk toward the overall accept rate (<see cref="Prior"/>) by
/// <see cref="PriorWeight"/> pseudo-results, so an unseen show scores as an average show rather than a
/// never-accepted one, and one lucky accept does not read as 100%. <see cref="ShowAcceptFeatures.SeenCount"/>
/// tells the model how much history the rate rests on.
/// </summary>
public sealed record ShowAcceptSmoothing(float Prior, float PriorWeight, int SeenCountCap)
{
    public const float DefaultPriorWeight = 5f;
    public const int DefaultSeenCountCap = 50;

    /// <summary>Models trained before smoothing: raw rate, unknown show = 0.</summary>
    public static ShowAcceptSmoothing Raw { get; } = new(0f, 0f, DefaultSeenCountCap);

    public ShowAcceptFeatures Features(ShowAcceptCounts? counts)
    {
        var accepted = counts?.Accepted ?? 0;
        var total = counts?.Total ?? 0;
        var denominator = total + PriorWeight;
        var rate = denominator <= 0 ? 0f : (accepted + Prior * PriorWeight) / denominator;
        var seen = (float)(Math.Log(1 + Math.Min(total, SeenCountCap)) / Math.Log(1 + SeenCountCap));
        return new ShowAcceptFeatures(rate, seen);
    }
}

public static class ShowAcceptHistory
{
    public static string? Key(string? showName) =>
        string.IsNullOrWhiteSpace(showName) ? null : showName.Trim();

    /// <summary>
    /// Point-in-time features for rows ordered by <c>began</c>: each row sees only results from earlier
    /// discovery runs, never its own label or later ones. Rows from the same run share history, because
    /// a curator closes a whole report at once.
    /// </summary>
    public static ShowAcceptFeatures[] PointInTime(
        IReadOnlyList<(string? ShowName, DateTime Began, bool Accepted)> rows,
        ShowAcceptSmoothing smoothing)
    {
        var counts = new Dictionary<string, ShowAcceptCounts>(StringComparer.OrdinalIgnoreCase);
        var features = new ShowAcceptFeatures[rows.Count];
        var index = 0;
        while (index < rows.Count)
        {
            var began = rows[index].Began;
            var end = index;
            while (end < rows.Count && rows[end].Began == began)
            {
                var key = Key(rows[end].ShowName);
                features[end] = smoothing.Features(key != null && counts.TryGetValue(key, out var c) ? c : null);
                end++;
            }

            for (var i = index; i < end; i++)
            {
                var key = Key(rows[i].ShowName);
                if (key == null)
                {
                    continue;
                }

                counts.TryGetValue(key, out var c);
                counts[key] = new ShowAcceptCounts(c.Accepted + (rows[i].Accepted ? 1 : 0), c.Total + 1);
            }

            index = end;
        }

        return features;
    }

    public static Dictionary<string, ShowAcceptCounts> Totals(IEnumerable<(string? ShowName, bool Accepted)> rows)
    {
        var counts = new Dictionary<string, ShowAcceptCounts>(StringComparer.OrdinalIgnoreCase);
        foreach (var (showName, accepted) in rows)
        {
            var key = Key(showName);
            if (key == null)
            {
                continue;
            }

            counts.TryGetValue(key, out var c);
            counts[key] = new ShowAcceptCounts(c.Accepted + (accepted ? 1 : 0), c.Total + 1);
        }

        return counts;
    }
}
