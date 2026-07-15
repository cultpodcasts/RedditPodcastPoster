using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Models;

/// <summary>
/// PodcastEpisodesResult excludes null entries and non-episode item types from the Spotify API.
/// </summary>
public class PodcastEpisodesResultRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When episode results include null entries, PodcastEpisodesResult exposes only typed episodes " +
        "because null slots must not surface to indexing.")]
    public void Excludes_null_entries_from_episodes()
    {
        // Arrange
        var episode = CreateEpisode("episode-1");
        var result = new PodcastEpisodesResult(new List<SimpleEpisode?> { episode, null }!);

        // Act
        var episodes = result.Episodes.ToList();

        // Assert
        episodes.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When episode results include items with a non-episode Type, PodcastEpisodesResult filters them out " +
        "because Spotify pages can contain mixed item kinds.")]
    public void Excludes_non_episode_item_types()
    {
        // Arrange
        var episode = CreateEpisode("episode-1");
        var nonEpisode = CreateEpisode("not-an-episode");
        nonEpisode.Type = ItemType.Track;
        var result = new PodcastEpisodesResult([episode, nonEpisode]);

        // Act
        var episodes = result.Episodes.ToList();

        // Assert
        episodes.Should().ContainSingle().Which.Id.Should().Be(episode.Id);
    }

    private SimpleEpisode CreateEpisode(string id) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };
}
