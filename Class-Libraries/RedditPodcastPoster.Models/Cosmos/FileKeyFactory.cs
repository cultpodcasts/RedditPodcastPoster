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

    private static readonly Regex AlphaNumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);
    private static readonly Regex TrailingNumberSuffixes = new(@"(_\d+)+$", RegexOptions.Compiled);

    /// <summary>
    /// The file-key prefix of <paramref name="kind"/>: empty for podcasts, otherwise <see cref="TvShowPrefix"/>,
    /// <see cref="NewsOrganisationPrefix"/> or <see cref="FilmPrefix"/>. The one kind-to-prefix map.
    /// </summary>
    public static string PrefixFor(FileKeyKind kind) => kind switch
    {
        FileKeyKind.Podcast => string.Empty,
        FileKeyKind.TvShow => TvShowPrefix,
        FileKeyKind.NewsOrganisation => NewsOrganisationPrefix,
        FileKeyKind.Film => FilmPrefix,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

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
    /// True when <paramref name="slug"/> (unprefixed) is a placeholder rather than a key: empty or whitespace, or
    /// with no ASCII letter or digit once any trailing <c>_N</c> suffixes are removed. So <c>""</c>, <c>"_"</c>,
    /// <c>"__2"</c>, <c>"_10"</c> are placeholders; <c>"x"</c>, <c>"24"</c> and <c>"foo_2"</c> are not.
    /// </summary>
    public static bool IsPlaceholderSlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug) ||
        !TrailingNumberSuffixes.Replace(slug, string.Empty).Any(char.IsAsciiLetterOrDigit);

    /// <summary>True when <paramref name="slug"/> (unprefixed) can be a file key: not a placeholder
    /// (<see cref="IsPlaceholderSlug"/>). One Latin letter or digit is enough.</summary>
    public static bool IsUsableSlug(string? slug) => !IsPlaceholderSlug(slug);

    /// <summary>
    /// True when <paramref name="fileKey"/> is usable once <paramref name="prefix"/> is removed: it carries the
    /// prefix (ignoring case) and the rest is a usable slug (<see cref="IsUsableSlug"/>). A bare prefix
    /// (<c>"news-"</c>) or a prefix plus a placeholder (<c>"news-_12"</c>) is not usable.
    /// </summary>
    public static bool IsUsableFileKey(string? fileKey, string prefix = "") =>
        fileKey is not null &&
        fileKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
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

    /// <summary><see cref="PrefixFor"/> of <paramref name="kind"/> plus
    /// <see cref="GetFileKey(string, Guid, string?)"/>.</summary>
    public static string GetFileKey(FileKeyKind kind, string name, Guid id, string? fallbackSlug = null) =>
        PrefixFor(kind) + GetFileKey(name, id, fallbackSlug);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetFilmFileKey(string title, Guid id, string? fallbackSlug = null) =>
        GetFileKey(FileKeyKind.Film, title, id, fallbackSlug);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetTvShowFileKey(string name, Guid id, string? fallbackSlug = null) =>
        GetFileKey(FileKeyKind.TvShow, name, id, fallbackSlug);

    /// <inheritdoc cref="GetFileKey(string, Guid, string?)"/>
    public static string GetNewsOrganisationFileKey(string name, Guid id, string? fallbackSlug = null) =>
        GetFileKey(FileKeyKind.NewsOrganisation, name, id, fallbackSlug);

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
