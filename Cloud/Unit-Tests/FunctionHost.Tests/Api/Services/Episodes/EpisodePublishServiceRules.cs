using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Episodes;

/// <summary>
/// Curator POST /episode/publish: podcast episodes and catalogue Film / NewsReport / TvShowEpisode.
/// </summary>
public class EpisodePublishServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _podcastResolved =
        new(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
    private CataloguePlayableResolution? _catalogueResolved;
    private Episode? _savedEpisode;
    private PodcastEpisode? _tweeted;
    private Uri? _tweetedUrl;
    private PodcastEpisode? _blueskyPosted;
    private Uri? _blueskyPostedUrl;
    private PodcastEpisode? _clearedCatalogue;
    private bool _episodeSaveCalled;
    private bool _shortnerWriteCalled;

    public EpisodePublishServiceRules()
    {
        _mocker.Use(NullLogger<EpisodePublishService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _podcastResolved);
        _mocker.GetMock<ICataloguePlayableResolver>()
            .Setup(r => r.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _catalogueResolved);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Callback<Episode>(episode =>
            {
                _episodeSaveCalled = true;
                _savedEpisode = episode;
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()))
            .Callback(() => _shortnerWriteCalled = true)
            .ReturnsAsync(new WriteResult(true, new Uri("https://cultpodcasts.com/e/x"), false));
        _mocker.GetMock<ITweetPoster>()
            .Setup(p => p.PostTweet(It.IsAny<PodcastEpisode>(), It.IsAny<Uri?>(), It.IsAny<bool>()))
            .Callback<PodcastEpisode, Uri?, bool>((pe, url, _) =>
            {
                _tweeted = pe;
                _tweetedUrl = url;
            })
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent, TweetId: "tw1"));
        _mocker.GetMock<IBlueskyPoster>()
            .Setup(p => p.Post(It.IsAny<PodcastEpisode>(), It.IsAny<Uri?>(), It.IsAny<bool>()))
            .Callback<PodcastEpisode, Uri?, bool>((pe, url, _) =>
            {
                _blueskyPosted = pe;
                _blueskyPostedUrl = url;
            })
            .ReturnsAsync(BlueskySendStatus.Success);
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(p => p.ClearIgnoredAndRemoved(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(pe => _clearedCatalogue = pe)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "When publishing a podcast episode tweet succeeds, then shorten via the shortener and save the episode document, because ignored/removed unblock and tweet state live on the episode container.")]
    public async Task podcast_tweet_success_saves_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcastResolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(podcast.Id, episode.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        result.Outcome!.Tweeted.Should().BeTrue();
        _shortnerWriteCalled.Should().BeTrue();
        _episodeSaveCalled.Should().BeTrue();
        _savedEpisode.Should().BeSameAs(episode);
        _tweeted.Should().NotBeNull();
        _tweeted!.IsCataloguePlayable.Should().BeFalse();
        _tweetedUrl.Should().Be(new Uri("https://cultpodcasts.com/e/x"));
        _clearedCatalogue.Should().BeNull();
    }

    [Fact(DisplayName =
        "When publishing a film tweet succeeds, then call the tweet poster with the catalogue site link and never save via the episode repository or shortener, because posters persist catalogue documents and film URLs are /film routes.")]
    public async Task film_tweet_success_does_not_save_episode()
    {
        // Arrange
        var film = CreateFilm();
        var projection = CatalogueParentKindMapper.ToPostProjection(film);
        var expectedUrl = CataloguePlayableSiteLink.For(projection);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.Film), projection, null, null, film.Name.Trim(), Removed: false);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, film.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        result.Outcome!.Tweeted.Should().BeTrue();
        result.Outcome.PodcastId.Should().Be(film.Id);
        _episodeSaveCalled.Should().BeFalse();
        _shortnerWriteCalled.Should().BeFalse();
        _tweeted.Should().NotBeNull();
        _tweeted!.IsCataloguePlayable.Should().BeTrue();
        _tweeted.ContentKind.Should().Be(nameof(ContentKind.Film));
        _tweetedUrl.Should().Be(expectedUrl);
        _clearedCatalogue.Should().BeNull();
    }

    [Fact(DisplayName =
        "When publishing a news report Bluesky post succeeds, then post the catalogue site link and never save via the episode repository or shortener, because the news report container owns post state and URLs are /news routes.")]
    public async Task news_bluesky_success_does_not_save_episode()
    {
        // Arrange
        var (organisation, report) = CreateNewsReport();
        var projection = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        var expectedUrl = CataloguePlayableSiteLink.For(projection);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.NewsReport), projection, organisation.Id, organisation.Name, organisation.Name,
            Removed: false);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, report.Id, new EpisodePublishRequest { BlueskyPost = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        result.Outcome!.BlueskyPosted.Should().BeTrue();
        result.Outcome.PodcastId.Should().Be(organisation.Id);
        _episodeSaveCalled.Should().BeFalse();
        _shortnerWriteCalled.Should().BeFalse();
        _blueskyPosted.Should().NotBeNull();
        _blueskyPosted!.ContentKind.Should().Be(nameof(ContentKind.NewsReport));
        _blueskyPostedUrl.Should().Be(expectedUrl);
    }

    [Fact(DisplayName =
        "When publishing a TV show episode tweet succeeds, then post the catalogue site link and never save via the episode repository or shortener, because the TV episode container owns post state and URLs are /tv routes.")]
    public async Task tv_tweet_success_does_not_save_episode()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var projection = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        var expectedUrl = CataloguePlayableSiteLink.For(projection);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.TvShowEpisode), projection, show.Id, show.Name, show.Name, Removed: false);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, tvEpisode.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        result.Outcome!.Tweeted.Should().BeTrue();
        result.Outcome.PodcastId.Should().Be(show.Id);
        _episodeSaveCalled.Should().BeFalse();
        _shortnerWriteCalled.Should().BeFalse();
        _tweeted!.ContentKind.Should().Be(nameof(ContentKind.TvShowEpisode));
        _tweetedUrl.Should().Be(expectedUrl);
    }

    [Fact(DisplayName =
        "When a film was ignored and a tweet succeeds, then clear ignored/removed on the catalogue document and still never save via the episode repository, because unblock must hit the film container.")]
    public async Task film_ignored_tweet_clears_catalogue_flags_without_episode_save()
    {
        // Arrange
        var film = CreateFilm();
        film.Ignored = true;
        var projection = CatalogueParentKindMapper.ToPostProjection(film);
        projection.Episode.Ignored.Should().BeTrue();
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.Film), projection, null, null, film.Name.Trim(), Removed: false);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, film.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        _episodeSaveCalled.Should().BeFalse();
        _shortnerWriteCalled.Should().BeFalse();
        _clearedCatalogue.Should().NotBeNull();
        _clearedCatalogue!.Episode.Id.Should().Be(film.Id);
        _tweetedUrl.Should().Be(CataloguePlayableSiteLink.For(projection));
    }

    [Fact(DisplayName =
        "When a film is removed and a tweet succeeds, then publish with the catalogue site link and clear ignored/removed, because item-level removed unblock matches podcast curator publish and Film has no parent.")]
    public async Task removed_film_tweet_publishes_and_clears()
    {
        // Arrange
        var film = CreateFilm();
        film.Removed = true;
        var projection = CatalogueParentKindMapper.ToPostProjection(film);
        projection.Episode.IsRemoved().Should().BeTrue();
        var expectedUrl = CataloguePlayableSiteLink.For(projection);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.Film), projection, null, null, film.Name.Trim(),
            Removed: true, ParentRemoved: false);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, film.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Ok);
        result.Outcome!.Tweeted.Should().BeTrue();
        _episodeSaveCalled.Should().BeFalse();
        _shortnerWriteCalled.Should().BeFalse();
        _tweetedUrl.Should().Be(expectedUrl);
        _clearedCatalogue.Should().NotBeNull();
        _clearedCatalogue!.Episode.Id.Should().Be(film.Id);
    }

    [Fact(DisplayName =
        "When a news report's organisation is removed, then publish fails without posting, because a removed parent publisher blocks like a removed podcast.")]
    public async Task parent_removed_news_publish_fails()
    {
        // Arrange
        var (organisation, report) = CreateNewsReport();
        organisation.Removed = true;
        var projection = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.NewsReport), projection, organisation.Id, organisation.Name, organisation.Name,
            Removed: true, ParentRemoved: true);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, report.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Failed);
        _tweeted.Should().BeNull();
        _shortnerWriteCalled.Should().BeFalse();
        _episodeSaveCalled.Should().BeFalse();
        _clearedCatalogue.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a TV episode's show is removed, then publish fails without posting, because a removed parent publisher blocks like a removed podcast.")]
    public async Task parent_removed_tv_publish_fails()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        show.Removed = true;
        var projection = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        _catalogueResolved = new CataloguePlayableResolution(
            nameof(ContentKind.TvShowEpisode), projection, show.Id, show.Name, show.Name,
            Removed: true, ParentRemoved: true);
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, tvEpisode.Id, new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Failed);
        _tweeted.Should().BeNull();
        _shortnerWriteCalled.Should().BeFalse();
        _episodeSaveCalled.Should().BeFalse();
        _clearedCatalogue.Should().BeNull();
    }

    [Fact(DisplayName =
        "When neither a podcast episode nor a catalogue playable exists, then publish fails, because there is nothing to post.")]
    public async Task missing_playable_fails()
    {
        // Arrange
        var sut = _mocker.CreateInstance<EpisodePublishService>();

        // Act
        var result = await sut.PublishAsync(
            new EpisodePublishRequestWrapper(null, _fixture.CreateGuid(), new EpisodePublishRequest { Tweet = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodePublishStatus.Failed);
        _episodeSaveCalled.Should().BeFalse();
        _tweeted.Should().BeNull();
    }

    private Film CreateFilm()
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(12));
        episode.Subjects = [];
        episode.Guests = null;
        return CatalogueParentKindMapper.ToFilm(podcast, episode);
    }

    private (RedditPodcastPoster.Models.News.NewsOrganisation Organisation,
        RedditPodcastPoster.Models.News.NewsReport Report) CreateNewsReport()
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(18));
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        return (organisation, CatalogueParentKindMapper.ToNewsReport(episode, organisation));
    }

    private (RedditPodcastPoster.Models.TvShows.TvShow Show,
        RedditPodcastPoster.Models.TvShows.TvShowEpisode Episode) CreateTvEpisode()
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(9));
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        return (show, CatalogueParentKindMapper.ToTvShowEpisode(episode, show));
    }
}
