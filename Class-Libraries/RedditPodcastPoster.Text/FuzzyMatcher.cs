using FuzzySharp;

namespace RedditPodcastPoster.Text;

public static class FuzzyMatcher
{
    public static T? Match<T>(string query, IEnumerable<T> items, Func<T, string> selector) where T : class
    {
        var closestMatch = Process.ExtractOne(query, items.Select(selector));
        if (closestMatch != null)
        {
            return items.FirstOrDefault(x => selector(x) == closestMatch.Value);
        }

        return null;
    }

    public static T? Match<T>(string query, IEnumerable<T> items, Func<T, string> selector, int min) where T : class
    {
        var closestMatch = Process.ExtractOne(query, items.Select(selector));
        if (closestMatch != null && closestMatch.Score >= min)
        {
            return items.FirstOrDefault(x => selector(x) == closestMatch.Value);
        }

        return null;
    }
}