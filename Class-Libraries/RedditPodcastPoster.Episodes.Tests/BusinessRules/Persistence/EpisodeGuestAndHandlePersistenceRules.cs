using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

/// <summary>
/// Guests must survive repository round-trips and merge.
/// Episode twitterHandles / blueskyHandles are retired.
/// </summary>
public class EpisodeGuestAndHandlePersistenceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "Saving an episode round-trips Guests without stripping the field.")]
    public async Task save_round_trip_preserves_guests()
    {
        // Arrange
        var repository = new InMemoryEpisodeRepository();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Guests = ["Janja Lalich", "Steven Hassan"];
            })
            .Create();

        // Act
        await repository.Save(episode);
        var stored = repository.GetStored(episode.Id);

        // Assert
        stored.Guests.Should().Equal("Janja Lalich", "Steven Hassan");
    }

    [Fact(DisplayName =
        "Merge fills missing platform links without clearing Guests on the stored episode.")]
    public void merge_preserves_guests_on_stored_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl)
            .Customize(e =>
            {
                e.Guests = ["Janja Lalich"];
                e.SpotifyId = string.Empty;
                e.Urls.Spotify = null;
            })
            .Create();

        var discovered = _fixture.BuildEpisode()
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl)
            .Create();

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.Guests.Should().Equal("Janja Lalich");
        stored.SpotifyId.Should().Be(spotifyInput.SpotifyId);
        stored.Urls.Spotify.Should().Be(spotifyInput.SpotifyUrl);
    }

    [Fact(DisplayName =
        "A newly discovered episode is added with a new ID and does not inherit Guests from an unrelated stored episode.")]
    public void discovered_episode_is_added_without_copying_unrelated_guests()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        stored.PodcastId = podcast.Id;
        stored.Guests = ["Someone Else"];

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        discovered.Guests.Should().BeNull();

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().ContainSingle().Which.Id.Should().NotBe(stored.Id);
        result.AddedEpisodes.Single().Guests.Should().BeNull();
        stored.Guests.Should().Equal("Someone Else");
    }
}
