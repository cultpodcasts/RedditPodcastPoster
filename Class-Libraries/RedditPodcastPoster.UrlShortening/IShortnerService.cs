using RedditPodcastPoster.Cloudflare;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlShortening;

public interface IShortnerService
{
    Task<WriteResult> Write(IEnumerable<PodcastEpisodeV2> podcastEpisodes);
    Task<WriteResult> Write(PodcastEpisodeV2 podcastEpisode, bool isDryRun = false);
    Task<KVRecord?> Read(string requestKey);
    Task<DeleteResult> Delete(PodcastEpisodeV2 podcastEpisode);
    Task<DeleteResult> Delete(IEnumerable<PodcastEpisodeV2> podcastEpisodes);
}