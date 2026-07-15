using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Enrichers;

/// <summary>
/// CLI AddAudioPodcast AddIdAndUrls fills missing podcast/episode Spotify ids and expensive-query flags from resolvers.
/// </summary>
public class SpotifyPodcastEnricherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the podcast SpotifyId is empty and FindPodcast returns a show id, AddIdAndUrls sets podcast.SpotifyId and returns true " +
        "because CLI enrich must persist the resolved Spotify show before episode fills.")]
    public async Task Empty_podcast_id_sets_spotify_id_from_resolver()
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = string.Empty);
        var podcastResolver = new Mock<ISpotifyPodcastResolver>();
        podcastResolver
            .Setup(x => x.FindPodcast(It.IsAny<FindSpotifyPodcastRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new SpotifyPodcastWrapper(simpleShow: new SimpleShow { Id = showId, Name = podcast.Name }));
        var episodeResolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(episodeResolver.Object, podcastResolver.Object);

        // Act
        var updated = await sut.AddIdAndUrls(podcast, [], new IndexingContext());

        // Assert
        updated.Should().BeTrue();
        podcast.SpotifyId.Should().Be(showId);
    }

    [Fact(DisplayName =
        "When FindPodcast returns ExpensiveQueryFound, AddIdAndUrls sets SpotifyEpisodesQueryIsExpensive " +
        "because subsequent indexer passes must honour the expensive-query guard for that show.")]
    public async Task Resolver_expensive_flag_sets_podcast_expensive_query()
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var wrapper = new SpotifyPodcastWrapper(simpleShow: new SimpleShow { Id = showId, Name = podcast.Name })
        {
            ExpensiveQueryFound = true
        };
        var podcastResolver = new Mock<ISpotifyPodcastResolver>();
        podcastResolver
            .Setup(x => x.FindPodcast(It.IsAny<FindSpotifyPodcastRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(wrapper);
        var episodeResolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(episodeResolver.Object, podcastResolver.Object);

        // Act
        await sut.AddIdAndUrls(podcast, [], new IndexingContext());

        // Assert
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
        podcast.SpotifyId.Should().Be(showId);
    }

    [Fact(DisplayName =
        "When the podcast already has a SpotifyId and an episode is missing SpotifyId, AddIdAndUrls calls FindEpisode and sets the episode id " +
        "because CLI enrich fills per-episode Spotify links after the show id is known.")]
    public async Task Missing_episode_id_is_filled_from_find_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = _fixture.CreateSpotifyId());
        var episode = _fixture.CreateEpisode(e =>
        {
            e.SpotifyId = string.Empty;
            e.Title = _fixture.CreateTitle();
            e.Release = DomainTestFixture.UtcDateDaysAgo(1);
            e.Length = _fixture.CreateDuration();
        });
        var resolvedEpisodeId = _fixture.CreateSpotifyId();
        var fullEpisode = new FullEpisode
        {
            Id = resolvedEpisodeId,
            Name = episode.Title,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd")
        };
        var podcastResolver = new Mock<ISpotifyPodcastResolver>(MockBehavior.Strict);
        var episodeResolver = new Mock<ISpotifyEpisodeResolver>();
        episodeResolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .ReturnsAsync(new FindEpisodeResponse(fullEpisode));
        var sut = CreateSut(episodeResolver.Object, podcastResolver.Object);

        // Act
        var updated = await sut.AddIdAndUrls(podcast, [episode], new IndexingContext());

        // Assert
        updated.Should().BeTrue();
        episode.SpotifyId.Should().Be(resolvedEpisodeId);
        episodeResolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When an episode already has a SpotifyId, AddIdAndUrls does not call FindEpisode for that episode " +
        "because CLI enrich must not re-resolve already linked rows.")]
    public async Task Episode_with_spotify_id_skips_find_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = _fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var podcastResolver = new Mock<ISpotifyPodcastResolver>(MockBehavior.Strict);
        var episodeResolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(episodeResolver.Object, podcastResolver.Object);

        // Act
        var updated = await sut.AddIdAndUrls(podcast, [episode], new IndexingContext());

        // Assert
        updated.Should().BeFalse();
        episodeResolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When FindPodcast returns null for an empty podcast SpotifyId, AddIdAndUrls returns false without mutating the podcast " +
        "because CLI enrich must not invent a show id on a miss.")]
    public async Task No_podcast_match_returns_false_without_mutation()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var podcastResolver = new Mock<ISpotifyPodcastResolver>();
        podcastResolver
            .Setup(x => x.FindPodcast(It.IsAny<FindSpotifyPodcastRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync((SpotifyPodcastWrapper?)null);
        var episodeResolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(episodeResolver.Object, podcastResolver.Object);

        // Act
        var updated = await sut.AddIdAndUrls(podcast, [], new IndexingContext());

        // Assert
        updated.Should().BeFalse();
        podcast.SpotifyId.Should().BeEmpty();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
    }

    private static SpotifyPodcastEnricher CreateSut(
        ISpotifyEpisodeResolver episodeResolver,
        ISpotifyPodcastResolver podcastResolver) =>
        new(
            episodeResolver,
            podcastResolver,
            NullLogger<SpotifyPodcastEnricher>.Instance);
}
