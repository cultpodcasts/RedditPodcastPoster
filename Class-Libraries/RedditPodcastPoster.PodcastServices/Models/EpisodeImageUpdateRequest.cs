namespace RedditPodcastPoster.PodcastServices.Models;

public record EpisodeImageUpdateRequest(
    bool? UpdateSpotifyImage= false,
    bool? UpdateAppleImage = false,
    bool? UpdateYouTubeImage = false,
    bool? UpdateBBCImage = false);
