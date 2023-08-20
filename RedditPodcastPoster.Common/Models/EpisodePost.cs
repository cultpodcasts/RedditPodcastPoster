namespace RedditPodcastPoster.Common.Models;

public record EpisodePost(
    string Title, 
    Uri? YouTube, 
    Uri? Spotify, 
    Uri? Apple, 
    string Release, 
    string Duration, 
    string Description, 
    string Id);