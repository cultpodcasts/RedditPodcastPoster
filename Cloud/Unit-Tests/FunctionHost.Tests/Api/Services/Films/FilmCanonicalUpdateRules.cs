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
        _mocker.Use(NullLogger<FilmUpdateService>.Instance);
        _mocker.Use(NullLogger<FilmGetService>.Instance);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => FilterAsync(_catalogue, selector));
    }

    [Fact(DisplayName =
        "POST film stores IMDb on IFilmCanonical, because several films share a display name and IMDb is the identity page.")]
    public async Task update_sets_imdb()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { Imdb = imdb.ToString() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Imdb.Should().Be(imdb);
    }

    [Fact(DisplayName =
        "POST film returns NotFound when the film id is missing, because identity links are stored on the film document.")]
    public async Task update_not_found_when_missing()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(filmId))
            .ReturnsAsync((Film?)null);
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(
                filmId,
                new FilmChangeRequest { Imdb = $"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "POST film with an empty IMDb string clears the stored URI, because the curator can remove a wrong identity link.")]
    public async Task update_clears_imdb_when_empty_string()
    {
        // Arrange
        var existing = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = existing
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { Imdb = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Imdb.Should().BeNull();
    }

    [Fact(DisplayName =
        "POST film that omits IMDb leaves the stored URI, because a missing JSON field is a no-op.")]
    public async Task update_omit_imdb_leaves_existing()
    {
        // Arrange
        var existing = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = existing
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest()),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Imdb.Should().Be(existing);
    }

    [Fact(DisplayName =
        "POST film returns BadRequest when IMDb is not an absolute http(s) URL, because identity links must be pasteable pages.")]
    public async Task update_rejects_relative_imdb()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { Imdb = "not-a-url" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET film by id returns Found with IMDb, because curator UIs resolve a known film document.")]
    public async Task get_by_id_returns_imdb()
    {
        // Arrange
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = imdb
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(film);
        var sut = _mocker.CreateInstance<FilmGetService>();

        // Act
        var result = await sut.GetAsync(film.Id.ToString(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmGetStatus.Found);
        result.Film.Should().NotBeNull();
        result.Film!.Imdb.Should().Be(imdb);
    }

    [Fact(DisplayName =
        "GET film by percent-encoded name returns Found, because route segments are decoded without treating plus as space.")]
    public async Task get_by_name_found_after_route_normalize()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var film = new Film(name) { Id = _fixture.CreateGuid() };
        _catalogue.Add(film);
        var sut = _mocker.CreateInstance<FilmGetService>();

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
        var sut = _mocker.CreateInstance<FilmGetService>();

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
        var sut = _mocker.CreateInstance<FilmGetService>();

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
