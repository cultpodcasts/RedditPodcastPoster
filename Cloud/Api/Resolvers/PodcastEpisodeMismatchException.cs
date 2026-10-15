namespace Api.Resolvers;

/// <summary>
/// Thrown when an episode found by its (globally unique) episode-id belongs to a podcast whose name
/// differs from the podcast name in the request route. This is a data/route inconsistency, not a normal not-found.
/// </summary>
public class PodcastEpisodeMismatchException(
    Guid episodeId,
    string routePodcastName,
    Guid actualPodcastId,
    string? actualPodcastName)
    : Exception(
        $"Episode with id '{episodeId}' belongs to podcast with id '{actualPodcastId}' and name '{actualPodcastName ?? MissingPodcastName}', which does not match the podcast name '{routePodcastName}' in the request.")
{
    /// <summary>Stands in for the actual podcast's name in the message when the episode's podcast no longer exists.</summary>
    public const string MissingPodcastName = "<podcast not found>";

    public Guid EpisodeId { get; } = episodeId;
    public string RoutePodcastName { get; } = routePodcastName;
    public Guid ActualPodcastId { get; } = actualPodcastId;
    public string? ActualPodcastName { get; } = actualPodcastName;
}
