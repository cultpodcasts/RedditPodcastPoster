namespace RedditPodcastPoster.Models;

public record PodcastPost(string Name,
    string TitleRegex,
    string DescriptionRegex,
    IEnumerable<EpisodePost> Episodes,
    Service? PodcastPrimaryPostService);