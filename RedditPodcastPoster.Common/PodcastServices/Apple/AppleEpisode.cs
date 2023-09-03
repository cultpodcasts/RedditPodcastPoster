namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public record AppleEpisode(
    long Id, 
    string Title, 
    DateTime Release, 
    TimeSpan Duration, 
    Uri Url, 
    string Description, 
    bool Explicit);