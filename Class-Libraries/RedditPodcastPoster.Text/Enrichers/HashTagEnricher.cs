using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text.Enrichers;

public class HashTagEnricher : IHashTagEnricher
{
    public (string Title, bool HashTagAdded) AddHashTag(string input, string match, string? replacement = null)
    {
        // A stored tag is literal text. Word-boundary anchors also miss a tag that ends on a symbol.
        var regex = new Regex(
            $"(#?(?<!\\w){Regex.Escape(match)}(?!\\w))",
            RegexOptions.IgnoreCase);
        var inputMatch = regex.Match(input);
        if (inputMatch.Success && !inputMatch.Captures.Single().Value.StartsWith("#"))
        {
            return (regex.Replace(input, $"#{replacement ?? match}", 1, 0), true);
        }

        return (input, false);
    }
}
