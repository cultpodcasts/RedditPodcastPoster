namespace RedditPodcastPoster.PodcastServices.Thumbnails;

/// <param name="Eligible">Recent episodes whose stored YouTube image is a withdrawable high-resolution tier.</param>
/// <param name="Probed">Stored images downloaded this run (capped by the probe budget).</param>
/// <param name="StillUsable">Probed images that are still real thumbnails.</param>
/// <param name="Replaced">Withdrawn images replaced with a usable thumbnail and saved.</param>
/// <param name="Unresolved">Withdrawn images where no better thumbnail was found; the stored image is kept.</param>
/// <param name="DeferredOverBudget">Eligible episodes left for a later run because a per-run budget was spent.</param>
public record YouTubeThumbnailRecheckResult(
    int Eligible = 0,
    int Probed = 0,
    int StillUsable = 0,
    int Replaced = 0,
    int Unresolved = 0,
    int DeferredOverBudget = 0,
    int Failed = 0)
{
    public static YouTubeThumbnailRecheckResult None { get; } = new();
}
