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

    /// <summary>
    /// Prefix of the slug used when a name gives no usable slug (for example a name written only in Korean,
    /// or only emoji). The rest is the first <see cref="IdSlugLength"/> hex digits of the item's id.
    /// </summary>
    public const string IdSlugPrefix = "id_";

    /// <summary>Hex digits of the id used by <see cref="GetIdSlug"/>.</summary>
    public const int IdSlugLength = 12;

    /// <summary>A slug needs at least this many ASCII letters or digits to be used as a file key.</summary>
    public const int MinimumSlugAlphaNumerics = 2;

    private static readonly Regex AlphaNumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);

    /// <summary>
    /// Unprefixed slug (podcast series and shared slug body). Only ASCII letters, digits and spaces survive,
    /// so a name with no Latin letters or digits gives an empty slug: use <see cref="GetFileKey(string, Guid, string?)"/>
    /// for any key that is stored.
    /// </summary>
    public static string GetFileKey(string name)
    {
        var alphaNumerics = AlphaNumerics.Replace(name, "");
        var removedSpacing = alphaNumerics.Replace("  ", "");
        var fileKey = removedSpacing.Replace(" ", "_").ToLower();
        return fileKey;
    }

    /// <summary>
    /// True when <paramref name="slug"/> (unprefixed) can be a file key: it has at least
    /// <see cref="MinimumSlugAlphaNumerics"/> ASCII letters or digits. Empty, underscore-only and one-character
    /// slugs (<c>""</c>, <c>"_"</c>, <c>"_2"</c>) are placeholders, not keys.
    /// </summary>
    public static bool IsUsableSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && slug.Count(char.IsAsciiLetterOrDigit) >= MinimumSlugAlphaNumerics;

    /// <summary>
    /// True when <paramref name="fileKey"/> is usable once <paramref name="prefix"/> is removed: it carries the
    /// prefix and the rest is a usable slug (<see cref="IsUsableSlug"/>).
    /// </summary>
    public static bool IsUsableFileKey(string? fileKey, string prefix = "") =>
        fileKey is not null &&
        fileKey.StartsWith(prefix, StringComparison.Ordinal) &&
        IsUsableSlug(fileKey[prefix.Length..]);

    /// <summary>Stable slug built from the id, for a name that gives no usable slug.</summary>
    public static string GetIdSlug(Guid id) => IdSlugPrefix + id.ToString("N")[..IdSlugLength];

    /// <summary>
    /// Unprefixed slug for a stored key. The name's slug when it is usable; otherwise
    /// <paramref name="fallbackSlug"/> when that is usable (for example the key a podcast already had before it moved
    /// kind); otherwise the id slug (<see cref="GetIdSlug"/>). Never empty, and the same inputs give the same slug.
    /// </summary>
    public static string GetFileKey(string name, Guid id, string? fallbackSlug = null)
    {
        var slug = GetFileKey(name ?? string.Empty);
        if (IsUsableSlug(slug))
        {
            return slug;
        }

        return IsUsableSlug(fallbackSlug) ? fallbackSlug! : GetIdSlug(id);
    }

    public static string GetFilmFileKey(string title) =>
        FilmPrefix + GetFileKey(title);

    public static string GetTvShowFileKey(string name) =>
        TvShowPrefix + GetFileKey(name);

    public static string GetNewsOrganisationFileKey(string name) =>
        NewsOrganisationPrefix + GetFileKey(name);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetFilmFileKey(string title, Guid id, string? fallbackSlug = null) =>
        FilmPrefix + GetFileKey(title, id, fallbackSlug);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetTvShowFileKey(string name, Guid id, string? fallbackSlug = null) =>
        TvShowPrefix + GetFileKey(name, id, fallbackSlug);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetNewsOrganisationFileKey(string name, Guid id, string? fallbackSlug = null) =>
        NewsOrganisationPrefix + GetFileKey(name, id, fallbackSlug);

    /// <summary>
    /// <paramref name="fileKey"/> when <paramref name="isTaken"/> says it is free, otherwise the first free
    /// <c>{fileKey}_2</c>, <c>{fileKey}_3</c>, ... (the suffix podcasts have always used).
    /// </summary>
    public static string MakeUnique(string fileKey, Func<string, bool> isTaken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        ArgumentNullException.ThrowIfNull(isTaken);
        if (!isTaken(fileKey))
        {
            return fileKey;
        }

        var counter = 2;
        string candidate;
        do
        {
            candidate = $"{fileKey}_{counter++}";
        } while (isTaken(candidate));

        return candidate;
    }
}
