namespace RedditPodcastPoster.BBC.Extractors;

public static class BbcSeriesName
{
    public static string? FromProgrammeBrand(string? primary, string episodeTitle)
    {
        if (string.IsNullOrWhiteSpace(primary))
        {
            return null;
        }

        var series = primary.Trim();
        if (string.Equals(series, episodeTitle.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return series;
    }

    public static string? FromDistinctCandidates(string? episodeTitle, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var series = candidate.Trim();
            if (!string.Equals(series, episodeTitle?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return series;
            }
        }

        return null;
    }
}
