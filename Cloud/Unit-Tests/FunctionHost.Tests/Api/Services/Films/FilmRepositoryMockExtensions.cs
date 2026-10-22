using Moq;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace FunctionHost.Tests.Api.Services.Films;

/// <summary>
/// For tests of a film write with no concurrent writer: the ETag-guarded <see cref="IFilmRepository.GetFilmForUpdate"/>
/// and <see cref="IFilmRepository.TryReplace"/> follow the test's own <see cref="IFilmRepository.GetFilm"/> and
/// <see cref="IFilmRepository.Save"/> setups, so those stay the single place a test arranges and observes the film.
/// </summary>
internal static class FilmRepositoryMockExtensions
{
    public const string SyntheticETag = "\"synthetic-etag\"";

    public static void ReplaceThroughSave(this Mock<IFilmRepository> films)
    {
        films
            .Setup(r => r.GetFilmForUpdate(It.IsAny<Guid>()))
            .Returns(async (Guid id) =>
                await films.Object.GetFilm(id) is { } film ? new FilmVersion(film, SyntheticETag) : null);
        films
            .Setup(r => r.TryReplace(It.IsAny<Film>(), It.IsAny<string>()))
            .Returns(async (Film film, string _) =>
            {
                await films.Object.Save(film);
                return true;
            });
    }
}
