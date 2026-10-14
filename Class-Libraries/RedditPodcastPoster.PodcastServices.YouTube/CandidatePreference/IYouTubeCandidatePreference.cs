namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Chooses between the finder's match and validated contenders for the same episode, scoring down
/// penalised titles (📱) and static-image uploads. Ties keep <paramref name="chosen"/>.
/// </summary>
public interface IYouTubeCandidatePreference
{
    Task<string> ChooseAsync(
        YouTubeCandidate chosen,
        IReadOnlyList<YouTubeCandidate> contenders,
        CancellationToken cancellationToken = default);
}