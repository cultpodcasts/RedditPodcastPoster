namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Episode language semantics. Authoritative product rules: docs/episode-language.md.
/// </summary>
/// <remarks>
/// <para>
/// <b>HARD — read time:</b> When an <see cref="Episode"/> is present, use <see cref="Episode.Language"/> only.
/// <c>null</c> means English — <b>never</b> coalesce to <c>Podcast.Language</c>.
/// </para>
/// <para>
/// <b>HARD — podcast default change (API):</b> Update only episodes that still follow the
/// <em>previous</em> podcast default. Null is English, not “unset”: an English episode on a
/// non-English show must not pick up a new podcast default. See
/// <see cref="FollowsPodcastDefault"/> / <see cref="LanguageAfterPodcastDefaultChange"/>.
/// </para>
/// </remarks>
public static class EpisodeLanguageResolution
{
    /// <summary>
    /// Read-time language for enrichment / matching when an episode document may or may not be present.
    /// </summary>
    public static string? ForRead(Podcasts.Podcast podcast, Episode? episode) =>
        episode is not null ? episode.Language : podcast.Language;

    /// <summary>
    /// Read-time language for a known episode document. Null means English.
    /// </summary>
    public static string? ForEpisode(Episode episode) => episode.Language;

    /// <summary>
    /// Product English: null, blank, <c>en</c>, or <c>en-*</c>.
    /// </summary>
    public static bool IsEnglish(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return true;
        }

        var lower = language.Trim().ToLowerInvariant().Replace('_', '-');
        return lower is "en" || lower.StartsWith("en-", StringComparison.Ordinal);
    }

    /// <summary>
    /// Cosmos / index storage form: English → <c>null</c>; otherwise trimmed code.
    /// </summary>
    public static string? ToStoredLanguage(string? language) =>
        IsEnglish(language) ? null : language!.Trim();

    /// <summary>
    /// Whether the episode is still on the podcast's default language (no curator override).
    /// When the podcast default is English, following means episode language is English (null).
    /// When the podcast default is non-English, following means episode language equals that code;
    /// null episode language is an English override and does <b>not</b> follow.
    /// </summary>
    public static bool FollowsPodcastDefault(string? episodeLanguage, string? podcastDefaultLanguage)
    {
        if (IsEnglish(podcastDefaultLanguage))
        {
            return IsEnglish(episodeLanguage);
        }

        if (IsEnglish(episodeLanguage))
        {
            return false;
        }

        return string.Equals(
            episodeLanguage!.Trim(),
            podcastDefaultLanguage!.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Next <see cref="Episode.Language"/> after a podcast default language change.
    /// Episodes that do not follow the previous default are unchanged.
    /// </summary>
    public static string? LanguageAfterPodcastDefaultChange(
        string? episodeLanguage,
        string? previousPodcastDefaultLanguage,
        string? newPodcastDefaultLanguage)
    {
        if (!FollowsPodcastDefault(episodeLanguage, previousPodcastDefaultLanguage))
        {
            return ToStoredLanguage(episodeLanguage);
        }

        return ToStoredLanguage(newPodcastDefaultLanguage);
    }
}
