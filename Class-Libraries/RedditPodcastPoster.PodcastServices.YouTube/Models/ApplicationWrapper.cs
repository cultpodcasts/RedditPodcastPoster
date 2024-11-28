using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record ApplicationWrapper(Application Application, int Index, int Reattempts);