using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <param name="RefreshMeta">
/// Force streaming page meta extract even if the URL is already known (SubmitUrl <c>-r</c>).
/// Overwrite vs fill-missing enrichment is chosen at the composition root by which
/// <c>IEpisodeEnricher</c> is registered — not by this flag.
/// </param>
public record SubmitOptions(
    Guid? PodcastId,
    bool MatchOtherServices,
    bool PersistToDatabase = true,
    bool CreatePodcast = false,
    string? PodcastName = null,
    EpisodeCreationSource CreationSource = EpisodeCreationSource.SubmitUrl,
    NonPodcastServiceItemMetaData? PrefetchedMeta = null,
    bool RefreshMeta = false);
