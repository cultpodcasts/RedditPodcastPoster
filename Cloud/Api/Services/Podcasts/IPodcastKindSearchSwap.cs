using RedditPodcastPoster.Search.Models;

namespace Api.Services.Podcasts;

public interface IPodcastKindSearchSwap
{
    Task<bool> UploadAsync(IReadOnlyList<EpisodeSearchRecord> documents, CancellationToken cancellationToken);
}
