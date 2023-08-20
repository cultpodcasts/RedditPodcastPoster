namespace RedditPodcastPoster.Common.Models;

public record PodcastPost(
    string Name, 
    string TitleRegex, 
    string DescriptionRegex, 
    IEnumerable<EpisodePost> Episodes);