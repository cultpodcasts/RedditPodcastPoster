using RedditPodcastPoster.Models;

namespace KVWriter.Shortner;

public interface IShortnerService
{
    Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes);
}