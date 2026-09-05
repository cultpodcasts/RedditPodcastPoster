using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record SubmitOptions(
    Guid? PodcastId,
    bool MatchOtherServices,
    bool PersistToDatabase = true,
    bool CreatePodcast = false,
    string? PodcastName = null,
    EpisodeCreationSource CreationSource = EpisodeCreationSource.SubmitUrl,
    NonPodcastServiceItemMetaData? PrefetchedMeta = null,
    bool RefreshMeta = false);
