using FluentAssertions;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Films;

public class FilmTrailerFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Without YouTube, a trailer plays from a video link before an audio-only Spotify or Apple link.")]
    public void video_link_is_preferred_over_audio()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var video = new Uri($"https://archive.org/details/synthetic-{_fixture.CreateGuid():N}");
        episode.Services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Apple] = new() { Url = new Uri($"https://podcasts.apple.com/synthetic/{_fixture.CreateGuid():N}") },
            ["internetArchive"] = new() { Url = video }
        };

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Url.Should().Be(video);
        trailer.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "An episode with only an audio link still becomes a trailer, playing from that link.")]
    public void audio_link_is_used_when_it_is_the_only_one()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var audio = new Uri($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");
        episode.Services = new Dictionary<string, ServiceLink> { [ServiceKeys.Spotify] = new() { Url = audio } };

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Url.Should().Be(audio);
    }
}
