namespace RedditPodcastPoster.Discovery.ML.Services;

public readonly record struct ScoredLabel(float Probability, bool Accepted);

public readonly record struct ThresholdOutcome(
    float Threshold,
    int Shown,
    int Hidden,
    float Precision,
    float Recall);

public static class DiscoveryThresholds
{
    public static readonly float[] ReportThresholds = [0.02f, 0.05f, 0.1f, 0.2f, 0.3f, 0.5f];

    /// <summary>
    /// Highest hide threshold that still shows at least <paramref name="targetRecall"/> of accepted results
    /// (an item is shown when its probability is at or above the threshold).
    /// </summary>
    public static float ForTargetRecall(IReadOnlyCollection<ScoredLabel> scored, float targetRecall)
    {
        if (targetRecall is <= 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(targetRecall), targetRecall, "Target recall must be in (0, 1].");
        }

        var positives = scored.Where(x => x.Accepted).Select(x => x.Probability).OrderBy(x => x).ToArray();
        if (positives.Length == 0)
        {
            return 0f;
        }

        var allowedMisses = (int)Math.Floor((1d - targetRecall) * positives.Length + 1e-9);
        return positives[Math.Min(allowedMisses, positives.Length - 1)];
    }

    public static ThresholdOutcome Evaluate(IReadOnlyCollection<ScoredLabel> scored, float threshold)
    {
        var shown = scored.Count(x => x.Probability >= threshold);
        var tp = scored.Count(x => x.Probability >= threshold && x.Accepted);
        var positives = scored.Count(x => x.Accepted);
        return new ThresholdOutcome(
            threshold,
            shown,
            scored.Count - shown,
            shown == 0 ? 0f : (float)tp / shown,
            positives == 0 ? 0f : (float)tp / positives);
    }
}
