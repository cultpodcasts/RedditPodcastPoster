using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ITvShowRepository : IRepository<TvShow>, IFilterableRepository<TvShow>
{
    Task<TvShow?> GetTvShow(Guid tvShowId);
}
