using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Curator transfer of a Podcast parent. Film is not a parent and is not a member.
/// JSON names match the enum identifiers (<c>TvShow</c>, <c>NewsOrganisation</c>).
/// Integer JSON values are rejected (<c>allowIntegerValues: false</c>).
/// </summary>
[JsonConverter(typeof(CatalogueParentKindJsonConverter))]
public enum CatalogueParentKind
{
    TvShow,
    NewsOrganisation
}

/// <summary>
/// String-name only converter for <see cref="CatalogueParentKind"/>.
/// </summary>
public sealed class CatalogueParentKindJsonConverter()
    : JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false);
