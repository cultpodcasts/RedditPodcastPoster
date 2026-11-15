using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Films;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

/// <summary>RPP #1430: a film PATCH fills the film's empty fields from its identity (TMDB), never overwriting.</summary>
public class FilmIdentityMetadataUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Film? _saved;

    public FilmIdentityMetadataUpdateRules()
    {
        _mocker.GetMock<IFilmRepository>().ReplaceThroughSave();
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(new SearchCleanupMock().Cleanup);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film => _saved = film)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "Setting a film's identity ids fills its empty description, release, runtime, image and alternative titles " +
        "from TMDB, because a film created from its trailer (Faderhuset) otherwise shows 0:00:00 and no details.")]
    public async Task setting_ids_fills_empty_fields()
    {
        // Arrange
        var film = StoredFilm(new Film("Faderhuset") { Description = string.Empty });
        var tmdbId = _fixture.CreateTmdbId();
        var imdbId = _fixture.CreateImdbId();
        var metadata = Metadata(tmdbId);
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchFilmAsync(tmdbId, imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { TmdbId = tmdbId, ImdbId = imdbId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Description.Should().Be(metadata.Overview);
        _saved.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.ReleaseDate!.Value));
        _saved.Length.Should().Be(TimeSpan.FromMinutes(metadata.RuntimeMinutes!.Value));
        _saved.Image.Should().Be(metadata.Image);
        _saved.AlternativeTitles.Should().Equal("Father's House");
    }

    [Fact(DisplayName =
        "A film save never overwrites a value already set from TMDB: the description and runtime the curator sent stay, " +
        "and only the empty release is filled.")]
    public async Task set_fields_are_not_overwritten()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var film = StoredFilm(new Film(_fixture.CreateTitle()) { TmdbId = tmdbId });
        var description = _fixture.CreateTitle();
        var metadata = Metadata(tmdbId);
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchFilmAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(
                film.Id,
                new FilmChangeRequest { Description = description, Duration = "1:02:03" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.Description.Should().Be(description);
        _saved.Length.Should().Be(new TimeSpan(1, 2, 3));
        _saved.Release.Should().BeEquivalentTo(CatalogueRelease.FromDate(metadata.ReleaseDate!.Value));
    }

    [Fact(DisplayName =
        "A film with no identity id, or with no empty field and unchanged ids, makes no identity lookup.")]
    public async Task no_lookup_without_ids_or_gaps()
    {
        // Arrange
        var noIds = StoredFilm(new Film(_fixture.CreateTitle()));
        var complete = StoredFilm(new Film(_fixture.CreateTitle())
        {
            TmdbId = _fixture.CreateTmdbId(),
            Description = _fixture.CreateTitle(),
            Length = _fixture.CreateDuration(),
            Image = new Uri("https://image.tmdb.org/t/p/w1280/x.jpg"),
            AlternativeTitles = []
        });
        complete.SetRelease(CatalogueRelease.FromYear(2010));
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        await sut.UpdateAsync(
            new FilmChangeRequestWrapper(noIds.Id, new FilmChangeRequest { Explicit = true }), CancellationToken.None);
        await sut.UpdateAsync(
            new FilmChangeRequestWrapper(complete.Id, new FilmChangeRequest { Explicit = true }), CancellationToken.None);

        // Assert
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchFilmAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A failed identity lookup still saves the curator's edit, because TMDB being down must not lose a save.")]
    public async Task failed_lookup_still_saves()
    {
        // Arrange
        var film = StoredFilm(new Film(_fixture.CreateTitle()));
        var tmdbId = _fixture.CreateTmdbId();
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchFilmAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbMovieMetadata?)null);
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { TmdbId = tmdbId }), CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        _saved!.TmdbId.Should().Be(tmdbId);
        _saved.Length.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "A curator's alternative titles replace the list, dropping blanks, the film's name and repeats; [] clears it " +
        "and stays cleared (RPP #1431).")]
    public async Task curator_alternative_titles_replace_the_list()
    {
        // Arrange
        var film = StoredFilm(new Film("Faderhuset") { AlternativeTitles = ["Old"] });
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        await sut.UpdateAsync(
            new FilmChangeRequestWrapper(
                film.Id,
                new FilmChangeRequest { AlternativeTitles = [" Father's House ", "", "faderhuset", "father's house"] }),
            CancellationToken.None);
        var replaced = _saved!.AlternativeTitles!.ToList();
        await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { AlternativeTitles = [] }),
            CancellationToken.None);

        // Assert
        replaced.Should().Equal("Father's House");
        _saved!.AlternativeTitles.Should().NotBeNull().And.BeEmpty();
    }

    private Film StoredFilm(Film film)
    {
        film.Id = _fixture.CreateGuid();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(film.Id))
            .ReturnsAsync(() => _saved?.Id == film.Id ? _saved : film);
        return film;
    }

    private TmdbMovieMetadata Metadata(int tmdbId) =>
        new(
            tmdbId,
            "Father's House",
            "Faderhuset",
            _fixture.CreateTitle(),
            88,
            new DateOnly(2026, 10, 19),
            new Uri("https://image.tmdb.org/t/p/w1280/backdrop.jpg"),
            ["Faderhuset", "Father's House"]);
}
