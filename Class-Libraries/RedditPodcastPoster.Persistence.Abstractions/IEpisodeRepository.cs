using System.Linq.Expressions;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeRepository : IRepository<Episode>, IFilterableRepository<Episode>
{
    Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId, Expression<Func<Episode, bool>> selector);
    Task<Episode?> GetMostRecentByPodcastId(Guid podcastId);
    Task Save(IEnumerable<Episode> episodes);
    Task Delete(Guid podcastId, Guid episodeId);
}