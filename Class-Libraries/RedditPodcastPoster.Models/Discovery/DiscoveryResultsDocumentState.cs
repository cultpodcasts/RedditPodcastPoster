
namespace RedditPodcastPoster.Models.Discovery;

/// <summary>
/// Lifecycle of a discovery report: <see cref="Unprocessed"/> -> (<see cref="PendingSupervisor"/>) -> <see cref="Processed"/>.
/// Serialised by name. New values are appended so historic numeric values are unchanged.
/// </summary>
public enum DiscoveryResultsDocumentState
{
    None = 0,
    Unprocessed,
    Processed,

    /// <summary>
    /// A supervised curator submitted this report; it is held until a supervisor applies the submission.
    /// Not curatable by anyone else and not returned by <c>GetAllUnprocessed</c>.
    /// </summary>
    PendingSupervisor
}
