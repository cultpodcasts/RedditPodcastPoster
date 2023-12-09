using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text;

public class HashTagEnricher : IHashTagEnricher
{
    public (string, bool) AddHashTag(string input, string hashTagText)
    {
        var regex = new Regex($"\\b{hashTagText}\\b", RegexOptions.IgnoreCase);
        if (regex.IsMatch(input))
        {
            return (regex.Replace(input, $"#{hashTagText}", 1, 0), true);
        }
        else
        {
            return (input, false);
        }
    }
}