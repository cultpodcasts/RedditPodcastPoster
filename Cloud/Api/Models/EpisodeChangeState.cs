namespace Api.Models;

public class EpisodeChangeState
{
    public bool UnPost { get; set; }
    public bool UpdatedSubjects { get; set; }
    public bool UnTweet { get; set; }
    public bool UnBlueskyPost { get; set; }

    /// <summary>
    /// AT URI captured before <see cref="RedditPodcastPoster.Models.Episodes.Episode.ClearBlueskyPostState"/>,
    /// so <c>RemovePost</c> can delete by rkey after local state is cleared for save.
    /// Null when the episode only had the legacy <c>bluesky</c> flag (search fallback).
    /// </summary>
    public string? BlueskyPostUriToRemove { get; set; }

    public bool UpdateBBCImage { get; internal set; }
    public bool UpdateYouTubeImage { get; internal set; }
    public bool UpdateAppleImage { get; internal set; }
    public bool UpdateSpotifyImage { get; internal set; }
    public bool UpdateImages => UpdateAppleImage || UpdateBBCImage || UpdateSpotifyImage || UpdateYouTubeImage;
    public bool PublishHomepage { get; internal set; }
}