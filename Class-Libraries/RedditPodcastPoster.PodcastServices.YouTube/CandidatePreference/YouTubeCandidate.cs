namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>A validated YouTube candidate for one episode.</summary>
public record YouTubeCandidate(string VideoId, string Title, TimeSpan? Length);