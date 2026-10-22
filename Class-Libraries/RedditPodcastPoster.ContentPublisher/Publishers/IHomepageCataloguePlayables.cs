namespace RedditPodcastPoster.ContentPublisher.Publishers;

/// <summary>
/// Homepage reads for the non-podcast playables: news reports, TV show episodes and films.
/// Podcast episodes stay in <see cref="HomepagePublisher"/>.
/// </summary>
public interface IHomepageCataloguePlayables
{
    /// <summary>
    /// News reports, TV show episodes and films released on or after <paramref name="since"/> that are
    /// not removed or ignored and whose news organisation or TV show exists and is not removed.
    /// </summary>
    Task<IReadOnlyList<HomepageCataloguePlayable>> GetRecent(DateTime since, CancellationToken ct);

    /// <summary>Durations of every news report, TV show episode and film that is not removed or ignored (parent not removed).</summary>
    Task<IReadOnlyList<TimeSpan>> GetActiveDurations(CancellationToken ct);

    /// <summary>Count of every news report, TV show episode and film that is not removed (parent not removed).</summary>
    Task<int> CountActive(CancellationToken ct);
}
