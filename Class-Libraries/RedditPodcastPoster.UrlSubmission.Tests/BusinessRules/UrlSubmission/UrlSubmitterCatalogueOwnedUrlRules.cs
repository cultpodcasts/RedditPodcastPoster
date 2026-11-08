using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// A YouTube / Spotify / Apple URL already held by a film (as a service link or trailer), TV episode or news report
/// is not re-created as a podcast episode on submit (#1089). All ids, names and URLs are synthetic.
/// </summary>
public class UrlSubmitterCatalogueOwnedUrlRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When a youtu.be link to a video a film holds as a trailer is submitted, nothing is categorised or saved and the result is already-exists for that film, because the trailer's episode row was deleted on attach and must not come back.")]
    public async Task youtube_short_link_to_a_film_trailer_saves_nothing()
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
        _mocker.Use<IUrlMembershipLookup>(_mocker.CreateInstance<UrlMembershipLookup>());
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(
            new Uri($"https://youtu.be/{videoId}"),
            new IndexingContext(),
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.PodcastResult.Should().Be(SubmitResultState.None);
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(film.Id);
        result.Episode.Should().BeNull();
        VerifyNothingSubmitted();
    }

    [Fact(DisplayName =
        "When a film owns a YouTube URL and the curator files it under a TV show, the submit still stops as already-exists for the film, because a film-owned URL has no podcast-platform resubmit path.")]
    public async Task film_owned_url_with_a_parent_target_still_stops()
    {
        // Arrange
        var url = YouTubeUrl();
        var filmId = _fixture.CreateGuid();
        SetupOwner(url, new CatalogueServiceUrlOwner(SubmitClassification.Film, null, filmId));
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true,
            Parent: new CatalogueParentTarget(CatalogueParentKind.TvShow, _fixture.CreateGuid())));

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(filmId);
        VerifyNothingSubmitted();
    }

    [Theory(DisplayName =
        "When a TV episode or news report owns a YouTube URL and routing finds no parent for it, the submit stops as already-exists for that item instead of filing a podcast episode.")]
    [InlineData(SubmitClassification.TvShowEpisode)]
    [InlineData(SubmitClassification.NewsReport)]
    public async Task tv_or_news_owned_url_with_no_routed_parent_stops(string contentKind)
    {
        // Arrange
        var url = YouTubeUrl();
        var playableId = _fixture.CreateGuid();
        SetupOwner(url, new CatalogueServiceUrlOwner(contentKind, _fixture.CreateTitle(), playableId));
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(x => x.RouteByOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, null));
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(contentKind);
        result.PlayableId.Should().Be(playableId);
        VerifyNothingSubmitted();
    }

    [Fact(DisplayName =
        "When a TV episode owns a YouTube URL and routing finds its TV show, the submit still goes to that show, because the parent submitter matches and enriches the stored episode.")]
    public async Task tv_owned_url_routed_to_its_show_is_still_submitted_to_the_show()
    {
        // Arrange
        var url = YouTubeUrl();
        var show = new TvShow(_fixture.CreateTitle());
        var parent = CatalogueParentMatch.For(show);
        SetupOwner(url,
            new CatalogueServiceUrlOwner(SubmitClassification.TvShowEpisode, show.Name, _fixture.CreateGuid(), show.Id));
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(x => x.RouteByOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, parent));
        var enriched = new SubmitResult(SubmitResultState.Enriched, SubmitResultState.None,
            ContentKind: SubmitClassification.TvShowEpisode);
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(x => x.Submit(parent, url, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(enriched);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.Should().BeSameAs(enriched);
    }

    [Fact(DisplayName =
        "When a TV episode of one show owns a YouTube URL and routing finds a different show, the submit stops as already-exists, because the parent submitter only matches within the routed show and would create a second TV episode.")]
    public async Task tv_owned_url_routed_to_a_different_show_stops()
    {
        // Arrange
        var url = YouTubeUrl();
        var owningShow = new TvShow(_fixture.CreateTitle());
        var routedShow = new TvShow(_fixture.CreateTitle());
        var playableId = _fixture.CreateGuid();
        SetupOwner(url, new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, owningShow.Name, playableId, owningShow.Id));
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(x => x.RouteByOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, CatalogueParentMatch.For(routedShow)));
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.PlayableId.Should().Be(playableId);
        VerifyNothingSubmitted();
    }

    [Theory(DisplayName =
        "When the curator files a URL a TV episode owns under a TV show, it reaches the parent submitter only when that show is the owner's, because filing it under another show would duplicate the episode.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task tv_owned_url_targeted_at_a_show_is_submitted_only_to_its_own_show(bool targetIsOwner)
    {
        // Arrange
        var url = YouTubeUrl();
        var owningShow = new TvShow(_fixture.CreateTitle());
        var targetShow = targetIsOwner ? owningShow : new TvShow(_fixture.CreateTitle());
        var playableId = _fixture.CreateGuid();
        SetupOwner(url, new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, owningShow.Name, playableId, owningShow.Id));
        var target = CatalogueParentMatch.For(targetShow);
        _mocker.GetMock<ICatalogueParentLookup>()
            .Setup(x => x.FindById(CatalogueParentKind.TvShow, targetShow.Id))
            .ReturnsAsync(target);
        var enriched = new SubmitResult(SubmitResultState.Enriched, SubmitResultState.None,
            ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId);
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(x => x.Submit(target, url, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(enriched);
        var sut = CreateSut();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true,
            Parent: new CatalogueParentTarget(CatalogueParentKind.TvShow, targetShow.Id)));

        // Assert
        if (targetIsOwner)
        {
            result.Should().BeSameAs(enriched);
        }
        else
        {
            result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
            result.PlayableId.Should().Be(playableId);
            VerifyNothingSubmitted();
        }
    }

    [Fact(DisplayName =
        "When no film, TV episode or news report owns a YouTube URL, the submit goes on to categorise it as before.")]
    public async Task unowned_url_is_categorised()
    {
        // Arrange
        var url = YouTubeUrl();
        SetupOwner(url, null);
        _mocker.GetMock<IEpisodeUrlRouter>()
            .Setup(x => x.RouteByOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlRoute(null, null, null));
        _mocker.GetMock<IUrlCategoriser>()
            .Setup(c => c.Categorise(It.IsAny<Podcast?>(), url, It.IsAny<IndexingContext>(), It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()))
            .ReturnsAsync(new CategorisedItem(null, null, null, null, null, null, null, Service.YouTube));
        _mocker.GetMock<ICategorisedItemProcessor>()
            .Setup(p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.None));
        var sut = CreateSut();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        _mocker.GetMock<ICategorisedItemProcessor>().Verify(
            p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()),
            Times.Once);
    }

    /// <summary>The submitter with the real ownership guard over the (mocked or real) membership lookup.</summary>
    private UrlSubmitter CreateSut()
    {
        _mocker.Use<ICatalogueOwnershipGuard>(_mocker.CreateInstance<CatalogueOwnershipGuard>());
        return _mocker.CreateInstance<UrlSubmitter>();
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private void SetupOwner(Uri url, CatalogueServiceUrlOwner? owner) =>
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(x => x.FindCatalogueServiceUrlOwner(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

    private void VerifyNothingSubmitted()
    {
        _mocker.GetMock<IUrlCategoriser>().Verify(
            c => c.Categorise(
                It.IsAny<Podcast?>(),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<NonPodcastServiceItemMetaData?>(),
                It.IsAny<bool>()),
            Times.Never);
        _mocker.GetMock<ICategorisedItemProcessor>().Verify(
            p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()),
            Times.Never);
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(
            s => s.Submit(It.IsAny<CatalogueParentMatch>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()),
            Times.Never);
    }
}
