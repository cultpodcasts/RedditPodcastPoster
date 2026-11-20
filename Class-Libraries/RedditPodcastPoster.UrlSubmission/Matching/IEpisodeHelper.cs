using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

public interface IEpisodeHelper
{
    bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem);

    /// <summary>
    /// Service keys (<c>spotify</c>, <c>apple</c>) on which <paramref name="episode"/> carries a different id from a
    /// Spotify or Apple submit that would otherwise match it (#1416). Empty when there is no such conflict: the
    /// submitted service's own id must be one of the conflicts, the release and duration must agree under the #1445
    /// cross-platform rules (#1481: a title match alone is never a conflict), and the episode must match the submit
    /// by <see cref="IsMatchingEpisode"/> once the conflicting links are set aside.
    /// </summary>
    IReadOnlyList<string> ConflictingServiceKeys(Episode episode, CategorisedItem categorisedItem);
}
