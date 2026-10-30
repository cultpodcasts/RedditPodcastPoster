namespace RedditPodcastPoster.Catalogue.Authority;

/// <summary>
/// One plausible authority hit a curator may choose when search is ambiguous.
/// <see cref="Key"/> is opaque to the UI and is sent back as <c>selectedIdentityKey</c>.
/// </summary>
public sealed record CatalogueAuthorityCandidate(
    string Key,
    string Title,
    int? Year,
    string Kind,
    string Source,
    int? TmdbId,
    long? TvdbId,
    Uri? PageUrl = null,
    Uri? ThumbnailUrl = null,
    string? Description = null)
{
    public const string FilmKind = "Film";
    public const string TvShowKind = "TvShow";
    public const string TvShowEpisodeKind = "TvShowEpisode";
    public const string TmdbSource = "tmdb";
    public const string TvdbSource = "tvdb";

    public static string TmdbKey(int tmdbId) => $"tmdb:{tmdbId}";

    public static string TvdbKey(long tvdbId) => $"tvdb:{tvdbId}";

    public static bool TryParseKey(string? key, out int? tmdbId, out long? tvdbId)
    {
        tmdbId = null;
        tvdbId = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var trimmed = key.Trim();
        const string tmdbPrefix = "tmdb:";
        const string tvdbPrefix = "tvdb:";
        if (trimmed.StartsWith(tmdbPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(trimmed[tmdbPrefix.Length..], out var parsedTmdb) &&
            parsedTmdb > 0)
        {
            tmdbId = parsedTmdb;
            return true;
        }

        if (trimmed.StartsWith(tvdbPrefix, StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(trimmed[tvdbPrefix.Length..], out var parsedTvdb) &&
            parsedTvdb > 0)
        {
            tvdbId = parsedTvdb;
            return true;
        }

        return false;
    }
}
