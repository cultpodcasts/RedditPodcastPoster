using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Resolvers;

/// <summary>
/// CLI AddAudioPodcast FindPodcast resolves a show by known id or by search + finder, and respects SkipSpotifyUrlResolving.
/// </summary>
public class SpotifyPodcastResolverRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, FindPodcast returns null without calling the Spotify client " +
        "because rate-limit recovery must not start a CLI show lookup.")]
    public async Task Skip_spotify_url_resolving_returns_null_without_client_calls()
    {
        // Arrange
        var client = new Mock<ISpotifyClientWrapper>(MockBehavior.Strict);
        var sut = CreateSut(client.Object, Mock.Of<ISpotifySearchResultFinder>(), Mock.Of<ISpotifyPodcastEpisodesProvider>());
        var request = new FindSpotifyPodcastRequest(
            _fixture.CreateSpotifyId(),
            _fixture.CreateTitle(),
            []);

        // Act
        var result = await sut.FindPodcast(request, new IndexingContext { SkipSpotifyUrlResolving = true });

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When PodcastId is set and GetFullShow returns a show, FindPodcast wraps that FullShow " +
        "because a known Spotify show id is the authoritative CLI enrich target.")]
    public async Task Known_podcast_id_returns_full_show_wrapper()
    {
        // Arrange
        var podcastId = _fixture.CreateSpotifyId();
        var fullShow = new FullShow { Id = podcastId, Name = _fixture.CreateTitle() };
        var client = new Mock<ISpotifyClientWrapper>();
        client
            .Setup(x => x.GetFullShow(
                podcastId,
                It.IsAny<ShowRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullShow);
        var sut = CreateSut(client.Object, Mock.Of<ISpotifySearchResultFinder>(), Mock.Of<ISpotifyPodcastEpisodesProvider>());
        var request = new FindSpotifyPodcastRequest(podcastId, fullShow.Name, []);

        // Act
        var result = await sut.FindPodcast(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.FullShow.Should().BeSameAs(fullShow);
        result.Id.Should().Be(podcastId);
        client.Verify(
            x => x.GetSimpleShows(It.IsAny<SearchRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When GetFullShow misses and search yields finder candidates without request episodes, FindPodcast uses fuzzy name fallback " +
        "because CLI enrich must still attach a show id from search results.")]
    public async Task Miss_full_show_falls_back_to_fuzzy_matching_simple_show()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var showId = _fixture.CreateSpotifyId();
        var candidate = new SimpleShow { Id = showId, Name = showName };
        var client = new Mock<ISpotifyClientWrapper>();
        client
            .Setup(x => x.GetFullShow(
                It.IsAny<string>(),
                It.IsAny<ShowRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FullShow?)null);
        client
            .Setup(x => x.GetSimpleShows(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingPodcasts(showName, It.IsAny<List<SimpleShow>?>()))
            .Returns([candidate]);
        var sut = CreateSut(client.Object, finder.Object, Mock.Of<ISpotifyPodcastEpisodesProvider>());
        var request = new FindSpotifyPodcastRequest(string.Empty, showName, []);

        // Act
        var result = await sut.FindPodcast(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.SimpleShow.Should().BeSameAs(candidate);
        result.Id.Should().Be(showId);
        result.FullShow.Should().BeNull();
    }

    [Fact(DisplayName =
        "KNOWN: When the episodes provider reports ExpensiveQueryFound during episode-URL verification, FindPodcast still returns ExpensiveQueryFound=false " +
        "because SpotifyPodcastWrapper's constructor accepts the flag but does not assign the init property.")]
    public async Task Expensive_query_found_is_not_assigned_on_wrapper()
    {
        // Arrange
        // KNOWN: likely bug â€” SpotifyPodcastWrapper(ctor expensiveQueryFound) never sets ExpensiveQueryFound.
        var showName = _fixture.CreateTitle();
        var showId = _fixture.CreateSpotifyId();
        var candidate = new SimpleShow { Id = showId, Name = showName };
        var episodeTitle = _fixture.CreateTitle();
        var episodeId = _fixture.CreateSpotifyId();
        var episodeUrl = _fixture.DefaultSpotifyUrl(episodeId);
        var matchingEpisode = new SimpleEpisode
        {
            Id = episodeId,
            Name = episodeTitle,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = episodeUrl.ToString() },
            Type = ItemType.Episode
        };
        var client = new Mock<ISpotifyClientWrapper>();
        client
            .Setup(x => x.GetFullShow(
                It.IsAny<string>(),
                It.IsAny<ShowRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FullShow?)null);
        client
            .Setup(x => x.GetSimpleShows(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingPodcasts(showName, It.IsAny<List<SimpleShow>?>()))
            .Returns([candidate]);
        finder
            .Setup(x => x.FindMatchingEpisodeByDate(
                episodeTitle.Trim(),
                It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<SimpleEpisode>>()))
            .Returns(matchingEpisode);
        var episodesProvider = new Mock<ISpotifyPodcastEpisodesProvider>();
        episodesProvider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new PodcastEpisodesResult([matchingEpisode], expensiveQueryFound: true));
        var sut = CreateSut(client.Object, finder.Object, episodesProvider.Object);
        var request = new FindSpotifyPodcastRequest(
            string.Empty,
            showName,
            [new FindSpotifyPodcastRequestEpisodes(DomainTestFixture.UtcDateDaysAgo(1), episodeUrl, episodeTitle)]);

        // Act
        var result = await sut.FindPodcast(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(showId);
        result.ExpensiveQueryFound.Should().BeFalse();
    }

    private static SpotifyPodcastResolver CreateSut(
        ISpotifyClientWrapper client,
        ISpotifySearchResultFinder finder,
        ISpotifyPodcastEpisodesProvider episodesProvider) =>
        new(
            client,
            finder,
            episodesProvider,
            NullLogger<SpotifyPodcastResolver>.Instance);
}
