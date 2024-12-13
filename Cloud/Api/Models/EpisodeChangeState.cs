namespace Api.Models;

public class EpisodeChangeState
{
    public bool UnPost { get; set; }
    public bool UpdatedSubjects { get; set; }
    public bool UnTweet { get; set; }
    public bool UnBlueskyPost { get; set; }
    public bool UpdateBBCImage { get; internal set; }
    public bool UpdateYouTubeImage { get; internal set; }
    public bool UpdateAppleImage { get; internal set; }
    public bool UpdateSpotifyImage { get; internal set; }
    public bool UpdateImages => UpdateAppleImage || UpdateBBCImage || UpdateSpotifyImage || UpdateYouTubeImage;
}