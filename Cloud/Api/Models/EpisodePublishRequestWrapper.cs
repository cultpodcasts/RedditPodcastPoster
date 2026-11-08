using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Models;

/// <param name="ExpectedKind">
/// When Film / NewsReport / TvShowEpisode (kind publish routes), resolve only that catalogue
/// container — skip podcast lookup and reject kind mismatch. Null keeps guid-only podcast-then-catalogue.
/// </param>
public record EpisodePublishRequestWrapper(
    Guid? PodcastId,
    Guid EpisodeId,
    EpisodePublishRequest EpisodePublishRequest,
    ContentKind? ExpectedKind = null);