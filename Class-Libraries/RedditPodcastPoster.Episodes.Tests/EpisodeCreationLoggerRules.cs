using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests;

public class EpisodeCreationLoggerRules
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName = "FormatMessage uses stable Episode created: prefix and includes ids/urls.")]
    public void format_message_includes_provenance_and_urls()
    {
        // Arrange
        var episodeId = _fixture.Create<Guid>();
        var podcastId = _fixture.Create<Guid>();
        var title = _fixture.Create<string>();
        var spotifyId = _fixture.Create<string>();
        var youTubeId = _fixture.Create<string>();
        var appleId = _fixture.Create<long>();
        var spotifyUrl = _fixture.Create<Uri>();
        var youTubeUrl = _fixture.Create<Uri>();
        var appleUrl = _fixture.Create<Uri>();

        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = title,
            Ids = new EpisodeIds
            {
                Spotify = spotifyId,
                YouTube = youTubeId,
                Apple = appleId
            },
            Services = new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = spotifyUrl },
                [ServiceKeys.YouTube] = new() { Url = youTubeUrl },
                [ServiceKeys.Apple] = new() { Url = appleUrl }
            }
        };

        var caller = _fixture.Create<string>();

        // Act
        var message = EpisodeCreationLogger.FormatMessage(
            episode,
            podcastId,
            EpisodeCreationSource.Indexer,
            Service.Spotify,
            caller: caller);

        // Assert
        message.Should().StartWith(EpisodeCreationLogger.MessagePrefix);
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain($"title='{title}'");
        message.Should().Contain($"podcast-id='{podcastId}'");
        message.Should().Contain("source='Indexer'");
        message.Should().Contain($"caller='{caller}'");
        message.Should().Contain("service='Spotify'");
        message.Should().Contain($"spotify-id='{spotifyId}'");
        message.Should().Contain($"spotify-url='{spotifyUrl}'");
        message.Should().Contain($"youtube-id='{youTubeId}'");
        message.Should().Contain($"youtube-url='{youTubeUrl}'");
        message.Should().Contain($"apple-id='{appleId}'");
        message.Should().Contain($"apple-url='{appleUrl}'");
    }

    [Fact(DisplayName = "ResolveCreatingService returns sole present platform identity.")]
    public void resolve_creating_service_sole_identity()
    {
        // Arrange
        var spotifyOnly = new Episode { Ids = new EpisodeIds { Spotify = _fixture.Create<string>() } };
        var youTubeOnly = new Episode { Ids = new EpisodeIds { YouTube = _fixture.Create<string>() } };
        var appleOnly = new Episode { Ids = new EpisodeIds { Apple = _fixture.Create<long>() } };

        // Act & Assert
        EpisodeCreationLogger.ResolveCreatingService(spotifyOnly, Service.YouTube)
            .Should().Be(Service.Spotify);
        EpisodeCreationLogger.ResolveCreatingService(youTubeOnly, Service.Spotify)
            .Should().Be(Service.YouTube);
        EpisodeCreationLogger.ResolveCreatingService(appleOnly, Service.Spotify)
            .Should().Be(Service.Apple);
    }

    [Fact(DisplayName = "ResolveCreatingService prefers release authority when multiple ids present.")]
    public void resolve_creating_service_prefers_release_authority()
    {
        // Arrange
        var episode = new Episode
        {
            Ids = new EpisodeIds
            {
                Spotify = _fixture.Create<string>(),
                YouTube = _fixture.Create<string>()
            }
        };

        // Act & Assert
        EpisodeCreationLogger.ResolveCreatingService(episode, Service.YouTube)
            .Should().Be(Service.YouTube);
        EpisodeCreationLogger.ResolveCreatingService(episode, Service.Spotify)
            .Should().Be(Service.Spotify);
    }

    [Theory(DisplayName = "FormatMessage includes source enum name for KQL filtering.")]
    [InlineData(EpisodeCreationSource.Indexer)]
    [InlineData(EpisodeCreationSource.SubmitUrl)]
    [InlineData(EpisodeCreationSource.Discovery)]
    public void format_message_includes_source(EpisodeCreationSource source)
    {
        // Arrange
        var spotifyId = _fixture.Create<string>();
        var spotifyUrl = _fixture.Create<Uri>();
        var episode = new Episode
        {
            Id = _fixture.Create<Guid>(),
            Title = _fixture.Create<string>(),
            Ids = new EpisodeIds { Spotify = spotifyId },
            Services = new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = spotifyUrl }
            }
        };

        var caller = _fixture.Create<string>();

        // Act
        var message = EpisodeCreationLogger.FormatMessage(
            episode, _fixture.Create<Guid>(), source, Service.Spotify, caller: caller);

        // Assert
        message.Should().Contain($"source='{source}'");
        message.Should().Contain($"caller='{caller}'");
    }
}
