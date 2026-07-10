namespace RedditPodcastPoster.People;

/// <summary>
/// Expands, normalizes, and deduplicates social handles for post tagging.
/// Comparison is case-insensitive and ignores a leading '@'. First occurrence wins.
/// </summary>
public static class SocialHandleDeduplicator
{
    public static string[] Deduplicate(
        IEnumerable<string?> handles,
        IEnumerable<string?>? alreadyTagged = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (alreadyTagged != null)
        {
            foreach (var key in Expand(alreadyTagged).Select(ToComparisonKey))
            {
                seen.Add(key);
            }
        }

        var result = new List<string>();
        foreach (var handle in Expand(handles))
        {
            if (seen.Add(ToComparisonKey(handle)))
            {
                result.Add(handle);
            }
        }

        return result.ToArray();
    }

    private static IEnumerable<string> Expand(IEnumerable<string?> handles)
    {
        foreach (var handle in handles)
        {
            if (string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            foreach (var part in handle.Split(
                         (char[]?)null,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = part.StartsWith('@') ? part : $"@{part}";
                if (normalized.Length > 1)
                {
                    yield return normalized;
                }
            }
        }
    }

    private static string ToComparisonKey(string handle) => handle.TrimStart('@');
}
