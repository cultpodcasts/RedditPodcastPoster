using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<TvShow> _catalogue = [];
    private TvShow? _saved;

    public TvShowCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _saved = show)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) => FilterAsync(_catalogue, selector));
    }

    [Fact(DisplayName =
        "POST TV show stores IMDb, TheTVDB and TMDB ids on the parent, because those ids are the canonical identity not streaming services.")]
    public async Task update_sets_imdb_tvdb_and_tmdb_ids()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var imdbId = _fixture.CreateImdbId();
        var tvdbId = _fixture.CreateTvdbId();
        var tmdbId = _fixture.CreateTmdbId();
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { ImdbId = imdbId, TvdbId = tvdbId, TmdbId = tmdbId }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved.Should().NotBeNull();
        _saved!.ImdbId.Should().Be(imdbId);
        _saved.TvdbId.Should().Be(tvdbId);
        _saved.TmdbId.Should().Be(tmdbId);
    }

    [Fact(DisplayName =
        "POST TV show with an empty IMDb id clears IMDb and leaves TVDB and TMDB, because omitted JSON is a no-op.")]
    public async Task update_clears_imdb_id_when_empty_string()
    {
        // Arrange
        var existingTvdbId = _fixture.CreateTvdbId();
        var existingTmdbId = _fixture.CreateTmdbId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = _fixture.CreateImdbId(),
            TvdbId = existingTvdbId,
            TmdbId = existingTmdbId
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { ImdbId = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.ImdbId.Should().BeNull();
        _saved.TvdbId.Should().Be(existingTvdbId);
        _saved.TmdbId.Should().Be(existingTmdbId);
    }

    [Fact(DisplayName =
        "POST TV show with TVDB and TMDB id 0 clears both and leaves IMDb, because zero is the clear sentinel for numeric ids.")]
    public async Task update_clears_numeric_ids_when_zero()
    {
        // Arrange
        var existingImdbId = _fixture.CreateImdbId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = existingImdbId,
            TvdbId = _fixture.CreateTvdbId(),
            TmdbId = _fixture.CreateTmdbId()
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { TvdbId = 0, TmdbId = 0 }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.TvdbId.Should().BeNull();
        _saved.TmdbId.Should().BeNull();
        _saved.ImdbId.Should().Be(existingImdbId);
    }

    [Fact(DisplayName =
        "POST TV show returns BadRequest when the IMDb id is not tt followed by digits, because the id is the identity and a URL is not an id.")]
    public async Task update_rejects_malformed_imdb_id()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { ImdbId = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET TV show by id returns Found with the identity ids, because curator UIs resolve a known parent document.")]
    public async Task get_by_id_returns_identity_ids()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var tvdbId = _fixture.CreateTvdbId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            ImdbId = imdbId,
            TvdbId = tvdbId
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.GetAsync(show.Id.ToString(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.Found);
        result.TvShow.Should().NotBeNull();
        result.TvShow!.ImdbId.Should().Be(imdbId);
        result.TvShow.TvdbId.Should().Be(tvdbId);
    }
    [Fact(DisplayName =
        "GET TV show by percent-encoded name returns Found, because route segments are decoded without treating plus as space.")]
    public async Task get_by_name_found_after_route_normalize()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var show = new TvShow(name) { Id = _fixture.CreateGuid() };
        _catalogue.Add(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.GetAsync(Uri.EscapeDataString(name), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.Found);
        result.TvShow!.Id.Should().Be(show.Id);
    }

    [Fact(DisplayName =
        "GET TV show by name returns Conflict with each matching id when two shows share a display name, because IMDb/TVDB exist to disambiguate.")]
    public async Task get_by_name_conflicts_when_homonyms()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = new TvShow(name) { Id = _fixture.CreateGuid() };
        var second = new TvShow(name) { Id = _fixture.CreateGuid() };
        _catalogue.Add(first);
        _catalogue.Add(second);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.GetAsync(name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact(DisplayName =
        "GET TV show by name returns Conflict when two shows differ only by case, because homonyms must not hide behind exact equality.")]
    public async Task get_by_name_conflicts_when_case_variant_homonyms()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = new TvShow(name) { Id = _fixture.CreateGuid() };
        var second = new TvShow(name.ToUpperInvariant()) { Id = _fixture.CreateGuid() };
        _catalogue.Add(first);
        _catalogue.Add(second);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.GetAsync(name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact(DisplayName =
        "GET TV show by name returns NotFound when no publisher matches the decoded name.")]
    public async Task get_by_name_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.GetAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.NotFound);
        result.TvShow.Should().BeNull();
    }

    private static async IAsyncEnumerable<TvShow> FilterAsync(
        IEnumerable<TvShow> source,
        Expression<Func<TvShow, bool>> selector)
    {
        var predicate = selector.Compile();
        foreach (var show in source.Where(predicate))
        {
            yield return show;
        }

        await Task.CompletedTask;
    }
}
