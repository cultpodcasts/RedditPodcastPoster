using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Discovery submits (curator submit and supervisor apply) follow the toolbar's catalogue ownership rule: a discovery
/// result whose YouTube / Spotify / Apple URL a film, film trailer, TV episode or news report already stores is not
/// re-created as a podcast episode (#1089). All ids, names and URLs are synthetic.
/// </summary>
public class CatalogueOwnedDiscoveryUrlSubmitterRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When a discovery result's youtu.be link is a video a film holds as a trailer, the result is already-exists for that film and the discovery submitter is not called, because the trailer's episode row was deleted on attach and must not come back.")]
    public async Task discovery_result_with_a_film_trailer_url_creates_nothing()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var film = new Film(_fixture.CreateTitle())
        {
            Trailers =
            [
                new FilmTrailer
                {
                    Id = _fixture.CreateGuid(),
                    Title = _fixture.CreateTitle(),
                    Url = new Uri($"https://www.youtube.com/watch?v={videoId}")
                }
            ]
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) => predicate.Compile()(film) ? film : null);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<IUrlMembershipLookup>(_mocker.CreateInstance<UrlMembershipLookup>());
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(
            Result(youTube: new Uri($"https://youtu.be/{videoId}")),
            new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(film.Id);
        result.Episode.Should().BeNull();
        VerifyInnerNotCalled();
        _mocker.GetMock<IEpisodeUrlRouter>().Verify(
            x => x.RouteByOwner(It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
    }

    [Fact(DisplayName =
        "When only one of a discovery result's URLs (Spotify) is film-owned, the whole result is already-exists, because discovery matched the URLs as one item.")]
    public async Task one_film_owned_url_stops_the_whole_result()
    {
        // Arrange
        var spotify = SpotifyUrl();
        var youTube = YouTubeUrl();
        var filmId = _fixture.CreateGuid();
        SetupOwner(spotify, new CatalogueServiceUrlOwner(SubmitClassification.Film, null, filmId));
        SetupOwner(youTube, null);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(Result(youTube, spotify), new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        result.PlayableId.Should().Be(filmId);
        VerifyInnerNotCalled();
    }

    [Theory(DisplayName =
        "When a TV episode or news report owns a discovery result's YouTube URL and routing finds no parent for it, the result is already-exists for that item instead of becoming a podcast episode.")]
    [InlineData(SubmitClassification.TvShowEpisode)]
    [InlineData(SubmitClassification.NewsReport)]
    public async Task tv_or_news_owned_url_with_no_routed_parent_stops(string contentKind)
    {
        // Arrange
        var url = YouTubeUrl();
        var playableId = _fixture.CreateGuid();
        var parentId = _fixture.CreateGuid();
        SetupOwner(url, new CatalogueServiceUrlOwner(contentKind, _fixture.CreateTitle(), playableId, parentId));
        Route(url, null);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(Result(url), new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(contentKind);
        result.PlayableId.Should().Be(playableId);
        result.ParentId.Should().Be(parentId);
        VerifyInnerNotCalled();
    }

    [Fact(DisplayName =
        "When a TV episode of one show owns a discovery result's URL and routing finds a different show, the result is already-exists, because submitting under the routed show would create a second TV episode.")]
    public async Task tv_owned_url_routed_to_a_different_show_stops()
    {
        // Arrange
        var url = YouTubeUrl();
        var owningShow = new TvShow(_fixture.CreateTitle());
        SetupOwner(url, new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, owningShow.Name, _fixture.CreateGuid(), owningShow.Id));
        Route(url, CatalogueParentMatch.For(new TvShow(_fixture.CreateTitle())));
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(Result(url), new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        VerifyInnerNotCalled();
    }

    [Fact(DisplayName =
        "When a TV episode owns a discovery result's URL and routing finds its own show, the discovery submitter runs as before, because the parent submitter matches and enriches the stored episode.")]
    public async Task tv_owned_url_routed_to_its_own_show_is_submitted()
    {
        // Arrange
        var url = YouTubeUrl();
        var show = new TvShow(_fixture.CreateTitle());
        SetupOwner(url, new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, show.Name, _fixture.CreateGuid(), show.Id));
        Route(url, CatalogueParentMatch.For(show));
        var discoveryResult = Result(url);
        var innerResult = new DiscoverySubmitResult(DiscoverySubmitResultState.EnrichedEpisode,
            ContentKind: SubmitClassification.TvShowEpisode);
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(x => x.Submit(discoveryResult, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(innerResult);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(discoveryResult, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.Should().BeSameAs(innerResult);
    }

    [Fact(DisplayName =
        "When no film, TV episode or news report owns any of a discovery result's URLs, the discovery submitter runs as before and no extra routing is done.")]
    public async Task unowned_result_is_submitted_without_extra_routing()
    {
        // Arrange
        var youTube = YouTubeUrl();
        var spotify = SpotifyUrl();
        SetupOwner(youTube, null);
        SetupOwner(spotify, null);
        var discoveryResult = Result(youTube, spotify);
        var innerResult = new DiscoverySubmitResult(DiscoverySubmitResultState.CreatedEpisode);
        _mocker.GetMock<IDiscoveryUrlSubmitter>()
            .Setup(x => x.Submit(discoveryResult, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(innerResult);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(discoveryResult, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.Should().BeSameAs(innerResult);
        _mocker.GetMock<IEpisodeUrlRouter>().Verify(
            x => x.RouteByOwner(It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
    }

    /// <summary>The decorator with the real ownership guard over the (mocked or real) membership lookup.</summary>
    private CatalogueOwnedDiscoveryUrlSubmitter CreateSut()
    {
        _mocker.Use<ICatalogueOwnershipGuard>(_mocker.CreateInstance<CatalogueOwnershipGuard>());
        return _mocker.CreateInstance<CatalogueOwnedDiscoveryUrlSubmitter>();
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private Uri SpotifyUrl() => new($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");

    private static DiscoveryResult Result(Uri? youTube = null, Uri? spotify = null) =>
        new() { Urls = new DiscoveryResultUrls { YouTube = youTube, Spotify = spotify } };

    private void SetupOwner(Uri url, CatalogueServiceUrlOwner? owner) =>
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(x => x.FindCatalogueServiceUrlOwner(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

    private void Route(Uri url, CatalogueParentMatch? parent) =>
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(x => x.RouteByOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, parent));

    private void VerifyInnerNotCalled() =>
        _mocker.GetMock<IDiscoveryUrlSubmitter>().Verify(
            x => x.Submit(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            Times.Never);
}
