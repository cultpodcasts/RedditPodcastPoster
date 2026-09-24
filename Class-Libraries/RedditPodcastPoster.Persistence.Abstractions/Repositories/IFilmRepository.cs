using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFilmRepository : IRepository<Film>, IFilterableRepository<Film>
{
    Task<Film?> GetFilm(Guid filmId);
}
