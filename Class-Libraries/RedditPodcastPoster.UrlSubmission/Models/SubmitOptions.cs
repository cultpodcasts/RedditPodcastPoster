using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.Logging;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record SubmitOptions(
    Guid? PodcastId,
    bool MatchOtherServices,
    bool PersistToDatabase = true,
    bool CreatePodcast = false,
    string? PodcastName = null,
    EpisodeCreationSource CreationSource = EpisodeCreationSource.SubmitUrl);
