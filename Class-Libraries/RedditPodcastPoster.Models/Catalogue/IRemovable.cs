namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Soft-delete / catalogue visibility. Implemented by publishers and playables.
/// Not applied to retired non-catalogue types (e.g. RedditPost archive entity).
/// <para>
/// Playables keep non-nullable <c>bool Removed</c> so existing Cosmos LINQ
/// (<c>!x.Removed</c> / <c>x.Removed</c>) is unchanged. Publishers keep
/// <c>bool? Removed</c> with their established <c>IsDefined</c> query patterns.
/// Prefer <see cref="IsRemoved"/> for in-memory checks.
/// </para>
/// </summary>
public interface IRemovable
{
    bool IsRemoved();
}
