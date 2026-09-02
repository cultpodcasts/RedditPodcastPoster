using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastUrlCategoriserRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private ResolvedNonPodcastServiceItem? _resolvedNonPodcast;

    public NonPodcastUrlCategoriserRules()
    {
        _mocker.Use<IEpisodeRepository>(new InMemoryEpisodeRepository());
        _mocker.Use(NonPodcastSubmitAdapterResolverSupport.Create());
        _mocker.GetMock<INonPodcastServiceCategoriser>()
            .Setup(x => x.Resolve(It.IsAny<Podcast?>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() => _resolvedNonPodcast);
    }

    [Fact(DisplayName =
        "A BBC Sounds play URL is categorised as a non-podcast item with Other authority, " +
        "and Spotify/Apple/YouTube resolvers are not used because Sounds is not a podcast catalogue.")]
    public async Task sounds_url_is_other_authority_without_platform_matching()
    {
        // Arrange
        var url = BbcSoundsUrl();
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: url,
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: true);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem.Should().BeSameAs(_resolvedNonPodcast);
        categorised.ResolvedSpotifyItem.Should().BeNull();
        categorised.ResolvedAppleItem.Should().BeNull();
        categorised.ResolvedYouTubeItem.Should().BeNull();
        _mocker.GetMock<ISpotifyUrlCategoriser>()
            .Verify(
                x => x.Resolve(
                    It.IsAny<Podcast?>(),
                    It.IsAny<IEnumerable<RedditPodcastPoster.Models.Episodes.Episode>>(),
                    It.IsAny<Uri>(),
                    It.IsAny<IndexingContext>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A BBC iPlayer episode URL is categorised as a non-podcast item with Other authority.")]
    public async Task iplayer_url_is_other_authority()
    {
        // Arrange
        var url = BbcIplayerUrl();
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem!.BBCUrl.Should().Be(url);
    }

    [Fact(DisplayName =
        "An Internet Archive details URL is categorised as a non-podcast item with Other authority.")]
    public async Task archive_url_is_other_authority()
    {
        // Arrange
        var url = InternetArchiveUrl();
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.InternetArchive,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem!.InternetArchiveUrl.Should().Be(url);
    }

    [Fact(DisplayName =
        "When a series is supplied with a Sounds URL, that podcast is passed to the non-podcast categoriser " +
        "so the episode can be attached to the chosen series.")]
    public async Task supplied_podcast_is_forwarded_to_non_podcast_categoriser()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var podcast = _fixture.CreatePodcast();
        Podcast? capturedPodcast = null;
        _mocker.GetMock<INonPodcastServiceCategoriser>()
            .Setup(x => x.Resolve(It.IsAny<Podcast?>(), url, It.IsAny<IndexingContext>()))
            .Callback<Podcast?, Uri, IndexingContext>((p, _, _) => capturedPodcast = p)
            .ReturnsAsync(() => _resolvedNonPodcast);
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            podcast,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(podcast, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        capturedPodcast.Should().BeSameAs(podcast);
        categorised.MatchingPodcast.Should().BeSameAs(podcast);
    }

    [Fact(DisplayName =
        "A Vimeo video URL is categorised as a non-podcast item with Other authority, " +
        "the same submit path as BBC Sounds and Internet Archive.")]
    public async Task vimeo_url_is_other_authority()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Vimeo,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem.Should().BeSameAs(_resolvedNonPodcast);
    }

    [Fact(DisplayName =
        "A Netflix title URL is categorised as a non-podcast item with Other authority, " +
        "the same submit path as Vimeo, Sounds, and Internet Archive.")]
    public async Task netflix_url_is_other_authority()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Netflix,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem.Should().BeSameAs(_resolvedNonPodcast);
    }

    [Fact(DisplayName =
        "A BBC host URL that is not Sounds play or iPlayer episode is not a submit URL, " +
        "so news and other BBC pages are not categorised as non-podcast items.")]
    public async Task bbc_news_url_is_not_matched_to_a_service()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var act = async () => await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Could not match url*");
        _mocker.GetMock<INonPodcastServiceCategoriser>()
            .Verify(
                x => x.Resolve(It.IsAny<Podcast?>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>()),
                Times.Never);
    }

    [Fact(DisplayName =
        "A Prime Video detail URL is categorised as a non-podcast item with Other authority.")]
    public async Task prime_url_is_other_authority()
    {
        // Arrange
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        _resolvedNonPodcast = new ResolvedNonPodcastServiceItem(
            NonPodcastService.AmazonPrime,
            Url: url,
            Title: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<UrlCategoriser>();

        // Act
        var categorised = await sut.Categorise(null, url, new IndexingContext(), matchOtherServices: false);

        // Assert
        categorised.Authority.Should().Be(Service.Other);
        categorised.ResolvedNonPodcastServiceItem.Should().BeSameAs(_resolvedNonPodcast);
    }

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private Uri BbcIplayerUrl() =>
        new($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

    private Uri InternetArchiveUrl() =>
        new($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
}
