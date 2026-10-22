using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlShortening.Services;

public interface IShortnerService
{
    Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes);
    Task<WriteResult> Write(PodcastEpisode podcastEpisode, bool isDryRun = false);
    Task<KVRecord?> Read(string requestKey);
    Task<DeleteResult> Delete(PodcastEpisode podcastEpisode);
    Task<DeleteResult> Delete(IEnumerable<PodcastEpisode> podcastEpisodes);

    /// <summary>
    /// Points the episode's short link at another site path (for example <c>/film/{name}/{shortId}</c> after the
    /// episode became a film trailer). Writes the record even when one exists, keeping the episode's share metadata.
    /// </summary>
    Task<WriteResult> Redirect(PodcastEpisode podcastEpisode, string sitePath);
}
