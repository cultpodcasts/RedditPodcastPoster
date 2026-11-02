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

    [Fact(DisplayName =
        "When a Spotify URL is present but nested ids.spotify is null, the trailer still stores the Spotify id derived from the URL, because pre-backfill / partial rows must re-match Spotify paste and indexer re-find.")]
    public void spotify_url_without_nested_id_stores_id_from_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var spotifyUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        var expectedSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode)!;
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.SpotifyEpisodeId(episode).Should().BeNull();

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Services.Should().ContainKey(ServiceKeys.Spotify);
        trailer.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        trailer.Ids.Should().NotBeNull();
        trailer.Ids!.Spotify.Should().Be(expectedSpotifyId);
    }

    [Fact(DisplayName =
        "When an Apple URL is present but nested ids.apple is null, the trailer still stores the Apple id derived from the URL, because pre-backfill / partial rows must re-match Apple paste.")]
    public void apple_url_without_nested_id_stores_id_from_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var appleEpisodeId = _fixture.CreateAppleId();
        var appleUrl = new Uri(
            $"https://podcasts.apple.com/podcast/synthetic-show/id{_fixture.CreateAppleId()}?i={appleEpisodeId}");
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, appleUrl, null);
        EpisodeServicePresence.AppleEpisodeId(episode).Should().BeNull();

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Services.Should().ContainKey(ServiceKeys.Apple);
        trailer.Services![ServiceKeys.Apple].Url.Should().Be(appleUrl);
        trailer.Ids.Should().NotBeNull();
        trailer.Ids!.Apple.Should().Be(appleEpisodeId);
    }

    [Fact(DisplayName =
        "When a nested Spotify id is present without a Spotify URL, the trailer does not store that id, because ownership matching must not claim a platform the trailer does not expose.")]
    public void orphaned_spotify_id_without_url_is_not_stored()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var orphanedSpotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(episode, orphanedSpotifyId);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify).Should().BeNull();

        // Act
        var trailer = FilmTrailerFactory.From(episode, null);

        // Assert
        trailer!.Services.Should().NotBeNull();
        trailer.Services!.Should().NotContainKey(ServiceKeys.Spotify);
        trailer.Ids.Should().NotBeNull();
        trailer.Ids!.Spotify.Should().BeNull();
        trailer.Ids.YouTube.Should().Be(EpisodeServicePresence.YouTubeEpisodeId(episode));
    }
}
