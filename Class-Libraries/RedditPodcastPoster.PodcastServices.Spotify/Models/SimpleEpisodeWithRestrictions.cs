using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

/// <summary>
/// SpotifyAPI.Web 7.4.2 deserializes <see cref="SimpleEpisode.IsPlayable"/> but omits
/// <c>restrictions</c>. Subclass captures it for skip-logging only (not persisted on Episode).
/// </summary>
public class SimpleEpisodeWithRestrictions : SimpleEpisode
{
    /// <summary>
    /// Spotify restriction map; typically contains key <c>reason</c> (e.g. <c>payment_required</c>).
    /// </summary>
    public Dictionary<string, string>? Restrictions { get; set; }
}
