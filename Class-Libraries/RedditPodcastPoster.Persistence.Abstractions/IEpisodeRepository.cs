using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeRepository : IRepository<Episode>, IFilterableRepository<Episode>
{
    Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId);
    Task<int> Count(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId, Expression<Func<Episode, bool>> selector);
    Task<Episode?> GetMostRecentByPodcastId(Guid podcastId);
    Task Save(IEnumerable<Episode> episodes);
    Task Delete(Guid podcastId, Guid episodeId);
}