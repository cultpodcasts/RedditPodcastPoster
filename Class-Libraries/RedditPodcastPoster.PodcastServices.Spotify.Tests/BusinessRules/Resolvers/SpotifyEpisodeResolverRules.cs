using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
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
/// FindEpisode must short-circuit on SkipSpotifyUrlResolving, prefer GetFullEpisode when an episode id is known,
/// and after id miss / no id page the catalogue choosing by-date vs by-length before hydrating via GetFullEpisode.
/// </summary>
public class SpotifyEpisodeResolverRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, FindEpisode returns no episode without calling Spotify clients " +
        "because rate-limit recovery must not start another episode lookup.")]
    public async Task Skip_spotify_url_resolving_returns_empty_without_client_calls()
    {
        // Arrange
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>(MockBehavior.Strict);
        var wrapper = new Mock<ISpotifyClientWrapper>(MockBehavior.Strict);
        var finder = new Mock<ISpotifySearchResultFinder>(MockBehavior.Strict);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: _fixture.CreateSpotifyId(),
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false);
        var indexingContext = new IndexingContext { SkipSpotifyUrlResolving = true };

        // Act
        var result = await sut.FindEpisode(request, indexingContext);

        // Assert
        result.FullEpisode.Should().BeNull();
        result.IsExpensiveQuery.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set and GetFullEpisode succeeds, FindEpisode returns that episode without paging the show catalogue " +
        "because a direct episode-id lookup is cheaper than name/date matching.")]
    public async Task Direct_episode_id_returns_full_episode_without_catalogue_paging()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var fullEpisode = new FullEpisode { Id = episodeId, Name = _fixture.CreateTitle(), IsPlayable = true };
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>(MockBehavior.Strict);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                episodeId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullEpisode);
        var finder = new Mock<ISpotifySearchResultFinder>(MockBehavior.Strict);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: episodeId,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(fullEpisode);
        wrapper.Verify(
            x => x.GetFullEpisode(
                episodeId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        provider.Verify(
            x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is empty, FindEpisode pages GetAllEpisodes, matches by date, then hydrates via GetFullEpisode " +
        "because catalogue identity must be resolved before a full episode document is returned.")]
    public async Task No_episode_id_pages_catalogue_matches_by_date_then_hydrates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var released = DomainTestFixture.UtcDateDaysAgo(2);
        var matchId = _fixture.CreateSpotifyId();
        var catalogueEpisode = CreateSimpleEpisode(matchId, title, released);
        var hydrated = new FullEpisode { Id = matchId, Name = title, IsPlayable = true };
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>();
        provider
            .Setup(x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodesResult([catalogueEpisode]));
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hydrated);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingEpisodeByDate(title, released, It.IsAny<IEnumerable<SimpleEpisode>>()))
            .Returns(catalogueEpisode);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: title,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false,
            Length: TimeSpan.FromMinutes(60),
            ReleaseAuthority: Service.Spotify);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(hydrated);
        provider.Verify(
            x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()),
            Times.Once);
        finder.Verify(
            x => x.FindMatchingEpisodeByDate(title, released, It.IsAny<IEnumerable<SimpleEpisode>>()),
            Times.Once);
        finder.Verify(
            x => x.FindMatchingEpisodeByLength(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<IEnumerable<SimpleEpisode>>(),
                It.IsAny<Func<SimpleEpisode, bool>?>(),
                It.IsAny<Service?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool>()),
            Times.Never);
        wrapper.Verify(
            x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When EpisodeSpotifyId is set but GetFullEpisode returns null, FindEpisode falls through to GetAllEpisodes " +
        "because a stale or invalid stored id must not block catalogue name/date matching.")]
    public async Task Episode_id_miss_falls_through_to_catalogue_paging()
    {
        // Arrange
        var missingId = _fixture.CreateSpotifyId();
        var title = _fixture.CreateTitle();
        var released = DomainTestFixture.UtcDateDaysAgo(1);
        var matchId = _fixture.CreateSpotifyId();
        var catalogueEpisode = CreateSimpleEpisode(matchId, title, released);
        var hydrated = new FullEpisode { Id = matchId, Name = title, IsPlayable = true };
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>();
        provider
            .Setup(x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodesResult([catalogueEpisode]));
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                missingId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FullEpisode?)null);
        wrapper
            .Setup(x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hydrated);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingEpisodeByDate(title, released, It.IsAny<IEnumerable<SimpleEpisode>>()))
            .Returns(catalogueEpisode);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: missingId,
            EpisodeTitle: title,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(hydrated);
        provider.Verify(
            x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()),
            Times.Once);
        wrapper.Verify(
            x => x.GetFullEpisode(
                missingId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        wrapper.Verify(
            x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When ReleaseAuthority is YouTube and Length is set, FindEpisode matches by length after GetAllEpisodes " +
        "because YouTube-authority audio lookups tolerate release lag and prefer duration alignment.")]
    public async Task YouTube_authority_with_length_matches_by_length_then_hydrates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var length = TimeSpan.FromMinutes(58);
        var released = DomainTestFixture.UtcDateDaysAgo(3);
        var matchId = _fixture.CreateSpotifyId();
        var catalogueEpisode = CreateSimpleEpisode(matchId, title, released, length);
        var hydrated = new FullEpisode { Id = matchId, Name = title, IsPlayable = true };
        Func<SimpleEpisode, bool>? reducer = _ => true;
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>();
        provider
            .Setup(x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodesResult([catalogueEpisode]));
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hydrated);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingEpisodeByLength(
                title,
                length,
                It.IsAny<IEnumerable<SimpleEpisode>>(),
                reducer,
                Service.YouTube,
                released,
                false))
            .Returns(catalogueEpisode);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: title,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false,
            ReleaseAuthority: Service.YouTube,
            Length: length);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext(), reducer);

        // Assert
        result.FullEpisode.Should().BeSameAs(hydrated);
        finder.Verify(
            x => x.FindMatchingEpisodeByLength(
                title,
                length,
                It.IsAny<IEnumerable<SimpleEpisode>>(),
                reducer,
                Service.YouTube,
                released,
                false),
            Times.Once);
        finder.Verify(
            x => x.FindMatchingEpisodeByDate(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<SimpleEpisode>>()),
            Times.Never);
        wrapper.Verify(
            x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When EnrichingYouTubeDiscoveredEpisode is true and Length is set, FindEpisode matches by length after GetAllEpisodes " +
        "because YouTube-discovered enrichment must use duration-aware catalogue matching even on Spotify-primary shows.")]
    public async Task Enriching_youtube_discovered_episode_matches_by_length_then_hydrates()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var length = TimeSpan.FromMinutes(61);
        var released = DomainTestFixture.UtcDateDaysAgo(4);
        var matchId = _fixture.CreateSpotifyId();
        var catalogueEpisode = CreateSimpleEpisode(matchId, title, released, length);
        var hydrated = new FullEpisode { Id = matchId, Name = title, IsPlayable = true };
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>();
        provider
            .Setup(x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodesResult([catalogueEpisode], expensiveQueryFound: true));
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                matchId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hydrated);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingEpisodeByLength(
                title,
                length,
                It.IsAny<IEnumerable<SimpleEpisode>>(),
                null,
                Service.Spotify,
                released,
                true))
            .Returns(catalogueEpisode);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: title,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false,
            ReleaseAuthority: Service.Spotify,
            Length: length,
            EnrichingYouTubeDiscoveredEpisode: true);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeSameAs(hydrated);
        result.IsExpensiveQuery.Should().BeTrue();
        finder.Verify(
            x => x.FindMatchingEpisodeByLength(
                title,
                length,
                It.IsAny<IEnumerable<SimpleEpisode>>(),
                null,
                Service.Spotify,
                released,
                true),
            Times.Once);
        finder.Verify(
            x => x.FindMatchingEpisodeByDate(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IEnumerable<SimpleEpisode>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When catalogue matching finds no episode, FindEpisode skips GetFullEpisode hydration " +
        "because there is no Spotify episode id to fetch.")]
    public async Task No_catalogue_match_skips_full_episode_hydration()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var released = DomainTestFixture.UtcDateDaysAgo(1);
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>();
        provider
            .Setup(x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodesResult([]));
        var wrapper = new Mock<ISpotifyClientWrapper>(MockBehavior.Strict);
        var finder = new Mock<ISpotifySearchResultFinder>();
        finder
            .Setup(x => x.FindMatchingEpisodeByDate(title, released, It.IsAny<IEnumerable<SimpleEpisode>>()))
            .Returns((SimpleEpisode?)null);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: title,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        provider.Verify(
            x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When GetFullEpisode returns an episode with IsPlayable=false, FindEpisode returns no episode " +
        "because paywalled Spotify episodes must not be attached or enriched.")]
    public async Task Non_playable_full_episode_is_excluded()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var paywalled = new FullEpisode
        {
            Id = episodeId,
            Name = _fixture.CreateTitle(),
            IsPlayable = false
        };
        var provider = new Mock<ISpotifyPodcastEpisodesProvider>(MockBehavior.Strict);
        var wrapper = new Mock<ISpotifyClientWrapper>();
        wrapper
            .Setup(x => x.GetFullEpisode(
                episodeId,
                It.IsAny<EpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paywalled);
        var finder = new Mock<ISpotifySearchResultFinder>(MockBehavior.Strict);
        var sut = CreateSut(provider.Object, wrapper.Object, finder.Object);
        var request = new FindSpotifyEpisodeRequest(
            PodcastSpotifyId: _fixture.CreateSpotifyId(),
            PodcastName: _fixture.CreateTitle(),
            EpisodeSpotifyId: episodeId,
            EpisodeTitle: _fixture.CreateTitle(),
            Released: DomainTestFixture.UtcDateDaysAgo(1),
            HasExpensiveSpotifyEpisodesQuery: false);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.FullEpisode.Should().BeNull();
        provider.Verify(
            x => x.GetAllEpisodes(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private SimpleEpisode CreateSimpleEpisode(
        string id,
        string title,
        DateTime release,
        TimeSpan? length = null) =>
        new()
        {
            Id = id,
            Name = title,
            DurationMs = (int)(length ?? _fixture.CreateDuration()).TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = true
        };

    private static SpotifyEpisodeResolver CreateSut(
        ISpotifyPodcastEpisodesProvider provider,
        ISpotifyClientWrapper wrapper,
        ISpotifySearchResultFinder finder) =>
        new(provider, wrapper, finder, NullLogger<SpotifyEpisodeResolver>.Instance);
}
