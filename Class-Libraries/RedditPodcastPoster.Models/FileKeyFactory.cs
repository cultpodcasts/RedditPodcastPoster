using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models;

public static class FileKeyFactory
{
    private static readonly Regex AlphaNumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);

    public static string GetFileKey(string name)
    {
        var alphaNumerics = AlphaNumerics.Replace(name, "");
        var removedSpacing = alphaNumerics.Replace("  ", "");
        var fileKey = removedSpacing.Replace(" ", "_").ToLower();
        return fileKey;
    }
}