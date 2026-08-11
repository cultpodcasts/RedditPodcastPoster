namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Read-time episode language resolution. Authoritative product rules: docs/episode-language.md.
/// </summary>
/// <remarks>
/// <para>
/// <b>HARD:</b> When an <see cref="Episode"/> is present, use <see cref="Episode.Language"/> only.
/// <c>null</c> / blank means English — <b>never</b> coalesce to <c>Podcast.Language</c>
/// (<c>episode.Language ?? podcast.Language</c> or
/// <c>IsNullOrWhiteSpace(episode.Language) ? podcast.Language : episode.Language</c>).
/// That anti-pattern treats curator English on non-English shows as the podcast language and
/// corrupts search, enrichment ignores, and title-casing.
/// </para>
/// <para>
/// Podcast language is applied at <b>write/inherit</b> time
/// (<see cref="Episode.InheritLanguageFromPodcastIfUnset"/>), not at every read.
/// </para>
/// </remarks>
public static class EpisodeLanguageResolution
{
    /// <summary>
    /// Read-time language for enrichment / matching when an episode document may or may not be present.
    /// </summary>
    /// <param name="podcast">Podcast (used only when <paramref name="episode"/> is null).</param>
    /// <param name="episode">When non-null, its <see cref="Episode.Language"/> is used even if null (English).</param>
    public static string? ForRead(Podcasts.Podcast podcast, Episode? episode) =>
        episode is not null ? episode.Language : podcast.Language;

    /// <summary>
    /// Read-time language for a known episode document. Null means English.
    /// </summary>
    public static string? ForEpisode(Episode episode) => episode.Language;
}
