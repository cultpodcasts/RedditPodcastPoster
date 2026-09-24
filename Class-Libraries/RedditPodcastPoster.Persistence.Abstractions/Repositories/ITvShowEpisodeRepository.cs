using System.Linq.Expressions;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ITvShowEpisodeRepository : IRepository<TvShowEpisode>, IFilterableRepository<TvShowEpisode>
{
    Task<TvShowEpisode?> GetTvShowEpisode(Guid tvShowId, Guid episodeId);
    Task<int> Count(Guid tvShowId);
    IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId);
    IAsyncEnumerable<TvShowEpisode> GetByTvShowId(Guid tvShowId, Expression<Func<TvShowEpisode, bool>> selector);
    Task Save(IEnumerable<TvShowEpisode> episodes);
    Task Delete(Guid tvShowId, Guid episodeId);
}
