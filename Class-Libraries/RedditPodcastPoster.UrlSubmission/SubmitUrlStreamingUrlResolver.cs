using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// Picks a catalogue URL from <see cref="Episode.Services"/> for SubmitUrl refresh-by-episode-id.
/// Prefers submit-eligible streaming wire keys in search-encode order; ignores Spotify / Apple / YouTube
/// and submit-retired keys (e.g. Hulu).
/// </summary>
public static class SubmitUrlStreamingUrlResolver
{
    public static bool TryGetUrl(Episode episode, out Uri url)
    {
        ArgumentNullException.ThrowIfNull(episode);
        url = null!;

        if (episode.Services is not { Count: > 0 })
        {
            return false;
        }

        foreach (var key in StreamingServiceWire.SubmitEligibleKeys)
        {
            if (episode.Services.TryGetValue(key, out var link) && link.Url is not null)
            {
                url = link.Url;
                return true;
            }
        }

        return false;
    }
}
