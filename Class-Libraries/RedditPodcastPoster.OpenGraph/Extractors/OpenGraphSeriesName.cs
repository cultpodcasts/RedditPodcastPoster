namespace RedditPodcastPoster.OpenGraph.Extractors;

public static class OpenGraphSeriesName
{
    public static string? FromDistinctCandidates(string? episodeTitle, string? platformPublisher, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var series = candidate.Trim();
            if (string.Equals(series, episodeTitle?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(platformPublisher) &&
                string.Equals(series, platformPublisher.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return series;
        }

        return null;
    }
}
