using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text;

public class HashTagEnricher : IHashTagEnricher
{
    public (string Title, bool HashTagAdded) AddHashTag(string input, string match, string? replacement = null)
    {
        var regex = new Regex($"(#?\\b{match}\\b)", RegexOptions.IgnoreCase);
        var inputMatch = regex.Match(input);
        if (inputMatch.Success && !inputMatch.Captures.Single().Value.StartsWith("#"))
        {
            return (regex.Replace(input, $"#{replacement ?? match}", 1, 0), true);
        }

        return (input, false);
    }
}