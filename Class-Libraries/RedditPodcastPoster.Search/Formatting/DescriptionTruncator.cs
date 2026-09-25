using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Search.Formatting;

public static class DescriptionTruncator
{
    private const string Ellipsis = "\u2026"; // …

    private static readonly Regex CosmosField = new(
        "^[A-Za-z][A-Za-z0-9]*(\\.[A-Za-z][A-Za-z0-9]*)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    /// <summary>
    /// Cosmos SQL for <see cref="TruncateForSearch"/> on a document field such as
    /// <c>e.description</c>. A missing field becomes an empty string so a search merge can clear it.
    /// The cut prefers the last space inside the budget, matching the push path for space-separated text.
    /// Tab and newline breaks remain push-path only: Cosmos SQL has no LastIndexOf across several characters.
    /// </summary>
    public static string CosmosSql(string valueExpression, int maxLength = Constants.DescriptionSize)
    {
        if (string.IsNullOrWhiteSpace(valueExpression) || !CosmosField.IsMatch(valueExpression))
        {
            throw new ArgumentException(
                "Cosmos description SQL requires a field path such as e.description.",
                nameof(valueExpression));
        }

        if (maxLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "maxLength must be at least 1.");
        }

        var budget = maxLength - Ellipsis.Length;
        if (budget <= 0)
        {
            return $"IIF(NOT IS_DEFINED({valueExpression}) OR IS_NULL({valueExpression}) OR LENGTH(LTRIM(RTRIM({valueExpression}))) = 0, \"\", \"{Ellipsis}\")";
        }

        var half = budget / 2;
        var trimmed = $"LTRIM(RTRIM({valueExpression}))";
        var slice = $"SUBSTRING({trimmed}, 0, {budget})";
        var reversedSpace = $"INDEX_OF(REVERSE({slice}), \" \")";
        var lastSpace = $"IIF({reversedSpace} = -1, -1, {budget} - 1 - ({reversedSpace}))";
        var cut =
            $"IIF(({lastSpace}) > {half}, CONCAT(RTRIM(SUBSTRING({trimmed}, 0, ({lastSpace}))), \"{Ellipsis}\"), CONCAT({slice}, \"{Ellipsis}\"))";
        return $"(IIF(LENGTH({trimmed}) <= {maxLength}, {trimmed}, {cut}) ?? \"\")";
    }
}
