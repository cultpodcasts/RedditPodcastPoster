using Api.Resolvers;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.UrlShortening.Extensions;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Models;

/// <summary>
/// Which kind of playable an episode read model is, and where it lives on the site. Added to the episode GET
/// responses so a client can link any kind (the existing podcast fields are unchanged).
/// </summary>
/// <param name="ContentKind"><c>Episode</c>, <c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c>.</param>
/// <param name="ParentId">Podcast, news organisation or TV show id; null for a film.</param>
/// <param name="ParentName">Podcast, news organisation or TV show name; null for a film.</param>
/// <param name="Slug">Route segment: the parent name, or a film's own name.</param>
/// <param name="Path">Site path with the kind's short id (<see cref="CatalogueShortId.PlayablePath"/>),
/// e.g. <c>/news/{slug}/{shortId}</c>.</param>
public sealed record PlayableKind(string ContentKind, Guid? ParentId, string? ParentName, string Slug, string Path)
{
    public static PlayableKind ForPodcastEpisode(Guid episodeId, Podcast podcast)
    {
        var name = podcast.Name.Trim();
        return new PlayableKind(
            nameof(RedditPodcastPoster.Models.ContentKinds.ContentKind.Episode),
            podcast.Id,
            name,
            name,
            CatalogueShortId.PlayablePath(name, episodeId, nameof(RedditPodcastPoster.Models.ContentKinds.ContentKind.Episode)));
    }

    public static PlayableKind For(CataloguePlayableResolution resolution) =>
        new(
            resolution.ContentKind,
            resolution.ParentId,
            resolution.ParentName,
            resolution.Slug,
            CatalogueShortId.PlayablePath(resolution.Slug, resolution.Projection.Episode.Id, resolution.ContentKind));
}
