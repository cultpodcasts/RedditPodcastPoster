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
    private TvShow? _saved;

    public TvShowCanonicalUpdateRules()
    {
        _mocker.Use(_mocker.GetMock<ITvShowRepository>());
        _mocker.Use(NullLogger<TvShowUpdateService>.Instance);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _saved = show)
            .Returns(Task.CompletedTask);
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
        var sut = _mocker.CreateInstance<TvShowUpdateService>();

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
        "POST TV show with an empty IMDb string clears the stored URI, because the curator can remove a wrong identity link.")]
    public async Task update_clears_imdb_when_empty_string()
    {
        // Arrange
        var existing = new Uri($"https://www.imdb.com/title/tt{_fixture.CreateAppleId()}/");
        var show = new TvShow(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Imdb = existing
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        var sut = _mocker.CreateInstance<TvShowUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowChangeRequestWrapper(
                show.Id,
                new TvShowChangeRequest { Imdb = "" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.Imdb.Should().BeNull();
        _saved.Tvdb.Should().BeNull();
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
        var sut = _mocker.CreateInstance<TvShowUpdateService>();

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
        "GET TV show by name returns Conflict with each matching id when two shows share a display name, because IMDb/TVDB exist to disambiguate.")]
    public async Task get_by_name_conflicts_when_homonyms()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var first = new TvShow(name) { Id = _fixture.CreateGuid() };
        var second = new TvShow(name) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(AsAsync(first, second));
        var getService = new TvShowGetService(
            _mocker.GetMock<ITvShowRepository>().Object,
            NullLogger<TvShowGetService>.Instance);

        // Act
        var result = await getService.GetAsync(name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowGetStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
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
        var setUrl = new Uri($"https://www.thetvdb.com/series/{_fixture.CreateYouTubeId()}");
        var setOk = CanonicalUriPatch.TryApply(setUrl.ToString(), uri => target = uri, out _);

        // Assert
        omitOk.Should().BeTrue();
        afterOmit.Should().NotBeNull();
        clearOk.Should().BeTrue();
        target.Should().BeNull();
        setOk.Should().BeTrue();
        target.Should().Be(setUrl);
    }

    private static async IAsyncEnumerable<TvShow> AsAsync(params TvShow[] shows)
    {
        foreach (var show in shows)
        {
            yield return show;
        }

        await Task.CompletedTask;
    }
}
