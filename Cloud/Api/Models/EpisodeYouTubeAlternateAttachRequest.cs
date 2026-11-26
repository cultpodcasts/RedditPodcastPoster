using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Models;

public sealed record EpisodeYouTubeAlternateAttachRequest(ContentKind TargetKind, Guid TargetId);
