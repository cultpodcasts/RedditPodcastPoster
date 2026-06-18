using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed record IndexerKeyRingSessionStart(
    int StartPrimaryIndex,
    int InitialRingIndex,
    IReadOnlyList<ApplicationWrapper> Ring);
