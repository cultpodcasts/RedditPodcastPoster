using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// What the host needs to index a just-saved news report / TV episode immediately: the parent as a
/// transient podcast and the episode view of the playable (same id). The search key is the playable id.
/// </summary>
public sealed record CatalogueSearchSource(Podcast Parent, Episode Episode, CatalogueParentKind Kind);
