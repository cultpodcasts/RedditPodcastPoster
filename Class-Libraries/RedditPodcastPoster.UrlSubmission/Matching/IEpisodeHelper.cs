using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

public interface IEpisodeHelper
{
    bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem);

    /// <summary>
    /// Service keys (<c>spotify</c>, <c>apple</c>) on which <paramref name="episode"/> carries a different id from a
    /// Spotify or Apple submit that would otherwise match it (#1416). Empty when there is no such conflict: the
    /// submitted service's own id must be one of the conflicts, the episode must match the submit by
    /// <see cref="IsMatchingEpisode"/> once the conflicting links are set aside, and its release and length must also
    /// agree (#1481: a title match alone is never a conflict). A stored episode carrying YouTube uses the #1445 /
    /// #1472 YouTube↔audio rules (same day or publishing-delay aligned; greater of 5 min or 12%); an audio-only one
    /// uses the same-release window or same day and the greater of 2 min or 3%.
    /// </summary>
    IReadOnlyList<string> ConflictingServiceKeys(Episode episode, CategorisedItem categorisedItem);
}
