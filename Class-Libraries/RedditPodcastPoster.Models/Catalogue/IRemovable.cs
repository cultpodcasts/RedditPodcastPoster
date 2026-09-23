namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Soft-delete / catalogue visibility. Implemented by publishers and playables.
/// Not applied to retired non-catalogue types (e.g. RedditPost archive entity).
/// </summary>
public interface IRemovable
{
    bool? Removed { get; set; }

    bool IsRemoved();
}
