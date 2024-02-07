using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text;

public class HashTagEnricher : IHashTagEnricher
{
    public (string, bool) AddHashTag(string input, string match, string? replacement = null)
    {
        var regex = new Regex($"\\b{match}\\b", RegexOptions.IgnoreCase);
        if (regex.IsMatch(input))
        {
            return (regex.Replace(input, $"#{replacement ?? match}", 1, 0), true);
        }

        return (input, false);
    }
}