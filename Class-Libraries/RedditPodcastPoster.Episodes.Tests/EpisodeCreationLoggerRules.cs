using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests;

public class EpisodeCreationLoggerRules
{
    [Fact(DisplayName = "FormatMessage uses stable Episode created: prefix and includes ids/urls.")]
    public void format_message_includes_provenance_and_urls()
    {
        var episodeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var podcastId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = "Preacher Boys Episode",
            SpotifyId = "spotifyEp123",
            YouTubeId = "ytVid456",
            AppleId = 9876543210,
            Urls = new ServiceUrls
            {
                Spotify = new Uri("https://open.spotify.com/episode/spotifyEp123"),
                YouTube = new Uri("https://www.youtube.com/watch?v=ytVid456"),
                Apple = new Uri("https://podcasts.apple.com/us/podcast/id1?i=9876543210")
            }
        };

        var message = EpisodeCreationLogger.FormatMessage(
            episode,
            podcastId,
            EpisodeCreationSource.Indexer,
            Service.Spotify,
            caller: "FakeIndexerComponent.CreateEpisode");

        message.Should().StartWith(EpisodeCreationLogger.MessagePrefix);
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain("title='Preacher Boys Episode'");
        message.Should().Contain($"podcast-id='{podcastId}'");
        message.Should().Contain("source='Indexer'");
        message.Should().Contain("caller='FakeIndexerComponent.CreateEpisode'");
        message.Should().Contain("service='Spotify'");
        message.Should().Contain("spotify-id='spotifyEp123'");
        message.Should().Contain("spotify-url='https://open.spotify.com/episode/spotifyEp123'");
        message.Should().Contain("youtube-id='ytVid456'");
        message.Should().Contain("youtube-url='https://www.youtube.com/watch?v=ytVid456'");
        message.Should().Contain("apple-id='9876543210'");
        message.Should().Contain("apple-url='https://podcasts.apple.com/us/podcast/id1?i=9876543210'");
    }

    [Fact(DisplayName = "ResolveCreatingService returns sole present platform identity.")]
    public void resolve_creating_service_sole_identity()
    {
        var spotifyOnly = new Episode { SpotifyId = "sp1" };
        var youTubeOnly = new Episode { YouTubeId = "yt1" };
        var appleOnly = new Episode { AppleId = 1 };

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
        var episode = new Episode
        {
            SpotifyId = "sp1",
            YouTubeId = "yt1"
        };

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
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = "t",
            SpotifyId = "x",
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/x") }
        };

        var message = EpisodeCreationLogger.FormatMessage(
            episode, Guid.NewGuid(), source, Service.Spotify, caller: "FakeSubmitPath.Persist");

        message.Should().Contain($"source='{source}'");
        message.Should().Contain("caller='FakeSubmitPath.Persist'");
    }
}
