using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Film> _catalogue = [];
    private Film? _saved;

    public FilmCanonicalUpdateRules()
    {
        _mocker.GetMock<IFilmRepository>().ReplaceThroughSave();
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => FilterAsync(_catalogue, selector));
    }

    [Fact(DisplayName =
        "POST film stores the IMDb id on IFilmCanonical, because several films share a display name and the IMDb id is the identity.")]
    public async Task update_sets_imdb_id()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var imdbId = _fixture.CreateImdbId();
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { ImdbId = imdbId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "POST film returns NotFound when the film id is missing, because identity ids are stored on the film document.")]
    public async Task update_not_found_when_missing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(filmId))
            .ReturnsAsync((Film?)null);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(
                filmId,
                new FilmChangeRequest { ImdbId = _fixture.CreateImdbId() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "POST film with an empty IMDb id clears the stored id, because the curator can remove a wrong identity.")]
    public async Task update_clears_imdb_when_empty_string()
    {
        // Arrange
        var existing = _fixture.CreateImdbId();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = existing
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { ImdbId = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.ImdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "POST film with TMDB id 0 clears the stored TMDB id and leaves the IMDb id, because zero is the clear sentinel for numeric ids.")]
    public async Task update_clears_tmdb_id_when_zero()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = imdbId,
            TmdbId = _fixture.CreateTmdbId()
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { TmdbId = 0 }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.TmdbId.Should().BeNull();
        _saved.ImdbId.Should().Be(imdbId);
    }
    [Fact(DisplayName =
        "POST film that omits the IMDb id leaves the stored id, because a missing JSON field is a no-op.")]
    public async Task update_omit_imdb_leaves_existing()
    {
        // Arrange
        var existing = _fixture.CreateImdbId();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = existing
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest()),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.ImdbId.Should().Be(existing);
    }

    [Fact(DisplayName =
        "POST film returns BadRequest when the IMDb id is not tt followed by digits, because the id is the identity and a URL is not an id.")]
    public async Task update_rejects_malformed_imdb_id()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { ImdbId = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET film by id returns Found with the IMDb id, because curator UIs resolve a known film document.")]
    public async Task get_by_id_returns_imdb_id()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = imdbId
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.GetAsync(film.Id.ToString(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmGetStatus.Found);
        result.Film.Should().NotBeNull();
        result.Film!.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "GET film by percent-encoded name returns Found, because route segments are decoded without treating plus as space.")]
    public async Task get_by_name_found_after_route_normalize()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var film = new Film(name) { Id = _fixture.CreateGuid() };
        _catalogue.Add(film);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.GetAsync(Uri.EscapeDataString(name), CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmGetStatus.Found);
        result.Film!.Id.Should().Be(film.Id);
    }

    [Fact(DisplayName =
        "GET film by name returns Conflict when two films differ only by case, because IMDb exists to disambiguate homonyms.")]
    public async Task get_by_name_conflicts_when_case_variant_homonyms()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = new Film(name) { Id = _fixture.CreateGuid() };
        var second = new Film(name.ToUpperInvariant()) { Id = _fixture.CreateGuid() };
        _catalogue.Add(first);
        _catalogue.Add(second);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.GetAsync(name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmGetStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact(DisplayName =
        "GET film by name returns NotFound when no publisher matches the decoded name.")]
    public async Task get_by_name_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.GetAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmGetStatus.NotFound);
        result.Film.Should().BeNull();
    }

    private static async IAsyncEnumerable<Film> FilterAsync(
        IEnumerable<Film> source,
        Expression<Func<Film, bool>> selector)
    {
        var predicate = selector.Compile();
        foreach (var film in source.Where(predicate))
        {
            yield return film;
        }

        await Task.CompletedTask;
    }
}
