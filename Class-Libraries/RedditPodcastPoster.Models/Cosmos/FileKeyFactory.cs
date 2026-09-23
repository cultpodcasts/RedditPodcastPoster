using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models.Cosmos;

public static class FileKeyFactory
{
    /// <summary>Prefix for Film file keys in backups / public JSON DB (vs unprefixed podcast series keys).</summary>
    public const string FilmPrefix = "film-";

    /// <summary>Prefix for TvShow file keys.</summary>
    public const string TvShowPrefix = "tvshow-";

    /// <summary>Prefix for NewsOrganisation file keys.</summary>
    public const string NewsOrganisationPrefix = "news-";

    private static readonly Regex AlphaNumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);

    /// <summary>Unprefixed slug (podcast series and shared slug body).</summary>
    public static string GetFileKey(string name)
    {
        var alphaNumerics = AlphaNumerics.Replace(name, "");
        var removedSpacing = alphaNumerics.Replace("  ", "");
        var fileKey = removedSpacing.Replace(" ", "_").ToLower();
        return fileKey;
    }

    public static string GetFilmFileKey(string title) =>
        FilmPrefix + GetFileKey(title);

    public static string GetTvShowFileKey(string name) =>
        TvShowPrefix + GetFileKey(name);

    public static string GetNewsOrganisationFileKey(string name) =>
        NewsOrganisationPrefix + GetFileKey(name);
}
