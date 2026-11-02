using FluentAssertions;
using Api.Services.Films;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
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

    [Fact(DisplayName =
        "When a YouTube-sourced episode also has Spotify and Apple links, the trailer plays from YouTube and still stores every podcast-platform URL and id, because Spotify and Apple paste and a Spotify-only indexer re-find must recognise the trailer.")]
    public void youtube_sourced_trailer_keeps_spotify_and_apple_urls_and_ids()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(podcast);
        var appleInput = _fixture.CreateAppleCatalogueInput();
        EpisodeServicePresence.SetAppleIdentity(episode, appleInput.AppleId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, appleInput.AppleUrl, null);
        var youTubeUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube)!;
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Url.Should().Be(youTubeUrl);
        trailer.Ids.Should().NotBeNull();
        trailer.Ids!.YouTube.Should().Be(EpisodeServicePresence.YouTubeEpisodeId(episode));
        trailer.Ids.Spotify.Should().Be(EpisodeServicePresence.SpotifyEpisodeId(episode));
        trailer.Ids.Apple.Should().Be(appleInput.AppleId);
        trailer.Services.Should().NotBeNull();
        trailer.Services![ServiceKeys.YouTube].Url.Should().Be(youTubeUrl);
        trailer.Services[ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        trailer.Services[ServiceKeys.Apple].Url.Should().Be(appleInput.AppleUrl);
    }
}
