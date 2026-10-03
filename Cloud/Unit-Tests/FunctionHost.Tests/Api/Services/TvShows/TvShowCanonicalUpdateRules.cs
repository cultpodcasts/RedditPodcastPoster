using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
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
    private readonly List<TvShow> _catalogue = [];
    private TvShow? _saved;

    public TvShowCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _saved = show)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) => FilterAsync(_catalogue, selector));
    }

    [Fact(DisplayName =
        "POST TV show stores IMDb and TheTVDB URIs on the parent, because those pages are the canonical identity not streaming services.")]
    public async Task update_sets_imdb_and_tvdb()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var tvdb = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { Imdb = imdb.ToString(), Tvdb = tvdb.ToString() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved.Should().NotBeNull();
        _saved!.Imdb.Should().Be(imdb);
        _saved.Tvdb.Should().Be(tvdb);
    }

    [Fact(DisplayName =
        "POST TV show with an empty IMDb string clears IMDb and leaves TVDB, because omitted JSON is a no-op.")]
    public async Task update_clears_imdb_when_empty_string()
    {
        // Arrange
        var existingImdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var existingTvdb = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = existingImdb,
            Tvdb = existingTvdb
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { Imdb = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.Imdb.Should().BeNull();
        _saved.Tvdb.Should().Be(existingTvdb);
    }

    [Fact(DisplayName =
        "POST TV show returns BadRequest when IMDb is not an absolute http(s) URL, because identity links must be pasteable pages.")]
    public async Task update_rejects_relative_imdb()
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
                new TvShowChangeRequest { Imdb = "not-a-url" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET TV show by id returns Found with IMDb, because curator UIs resolve a known parent document.")]
    public async Task get_by_id_returns_imdb()
    {
        // Arrange
        var imdb = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = imdb
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
        result.TvShow!.Imdb.Should().Be(imdb);
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

    [Fact(DisplayName =
        "CanonicalUriPatch treats omitted JSON as no-op, blank as clear, and an http URL as set.")]
    public void canonical_uri_patch_omit_clear_and_set()
    {
        // Arrange
        Uri? target = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");

        // Act
        var omitOk = CanonicalUriPatch.TryApply(null, uri => target = uri, out _);
        var afterOmit = target;
        var clearOk = CanonicalUriPatch.TryApply("", uri => target = uri, out _);
        var afterClear = target;
        var setUrl = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var setOk = CanonicalUriPatch.TryApply(setUrl.ToString(), uri => target = uri, out _);

        // Assert
        omitOk.Should().BeTrue();
        afterOmit.Should().NotBeNull();
        clearOk.Should().BeTrue();
        afterClear.Should().BeNull();
        setOk.Should().BeTrue();
        target.Should().Be(setUrl);
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
