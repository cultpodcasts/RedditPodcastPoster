using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator transfer of a Podcast parent. Film is not a parent and is not a member.
/// JSON names match the enum identifiers (<c>TvShow</c>, <c>NewsOrganisation</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogueParentKind
{
    TvShow,
    NewsOrganisation
}
