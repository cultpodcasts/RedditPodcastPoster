namespace RedditPodcastPoster.Search;

public static class DescriptionTruncator
{
    private const string Ellipsis = "\u2026"; // …

    /// <summary>
    /// Truncates a description for the search index to at most <see cref="Constants.DescriptionSize"/>
    /// characters, preferring a word boundary and appending an ellipsis when truncated.
    /// </summary>
    public static string TruncateForSearch(string? description, int maxLength = Constants.DescriptionSize)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var trimmed = description.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        var budget = maxLength - Ellipsis.Length;
        if (budget <= 0)
        {
            return Ellipsis[..maxLength];
        }

        var slice = trimmed[..budget];
        var lastWhitespace = slice.LastIndexOfAny([' ', '\t', '\r', '\n']);
        if (lastWhitespace > budget / 2)
        {
            slice = slice[..lastWhitespace];
        }

        return slice.TrimEnd() + Ellipsis;
    }
}
