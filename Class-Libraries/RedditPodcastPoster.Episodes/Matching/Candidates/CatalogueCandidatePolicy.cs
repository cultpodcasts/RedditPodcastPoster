namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// One candidate-sourcing order for every audio platform:
/// <list type="number">
/// <item>Recent episodes (released within <see cref="RecentWindow"/>, or with no release date) with a
/// known show: match against one bounded page (<see cref="RecentShowEpisodesLimit"/>) of the show feed.
/// The feed does not depend on the incoming title, so a YouTube title that differs from the audio
/// title still finds its episode (Steve Hassan, 12 Oct 2026).</item>
/// <item>Otherwise, or when the recent page has no match: the platform's search fallback, minus
/// candidates already tried.</item>
/// </list>
/// Matching itself stays with the shared <see cref="IEpisodePlatformMatcher"/> passed in by the caller.
/// </summary>
public sealed class CatalogueCandidatePolicy(TimeProvider? timeProvider = null) : ICatalogueCandidatePolicy
{
    public const int RecentShowEpisodesLimit = 50;
    public static readonly TimeSpan RecentWindow = EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<TItem?> FindMatch<TItem>(
        DateTime? released,
        ICatalogueCandidateSource<TItem> source,
        Func<IReadOnlyList<TItem>, Task<TItem?>> match)
        where TItem : class
    {
        var tried = new HashSet<string>(StringComparer.Ordinal);
        if (source.HasShow && IsRecent(released))
        {
            var recent = await source.GetRecentShowEpisodes(RecentShowEpisodesLimit);
            foreach (var item in recent)
            {
                tried.Add(source.GetId(item));
            }

            if (recent.Count > 0)
            {
                var recentMatch = await match(recent);
                if (recentMatch != null)
                {
                    return recentMatch;
                }
            }
        }

        var searched = (await source.SearchEpisodes())
            .Where(x => !tried.Contains(source.GetId(x)))
            .ToList();
        return searched.Count == 0 ? null : await match(searched);
    }

    public bool IsRecent(DateTime? released) =>
        released is not { } value ||
        _timeProvider.GetUtcNow().UtcDateTime - value < RecentWindow;
}
