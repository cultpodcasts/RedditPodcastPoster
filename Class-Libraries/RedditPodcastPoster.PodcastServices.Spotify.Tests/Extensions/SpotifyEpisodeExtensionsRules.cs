using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Extensions;

/// <summary>
/// Spotify SDK DTOs mark ReleaseDate as non-nullable, but the API sometimes returns null or "0000".
/// </summary>
public class SpotifyEpisodeExtensionsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "When SimpleEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue " +
        "because invalid release strings must not break indexing.")]
    [InlineData(null)]
    [InlineData("0000")]
    public void Simple_episode_invalid_release_date_returns_min_value(string? releaseDate)
    {
        // Arrange
        var episode = CreateSimpleEpisode(releaseDate);

        // Act
        var result = episode.GetReleaseDate();

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [Fact(DisplayName =
        "When SimpleEpisode ReleaseDate is a valid Spotify date string, GetReleaseDate parses it " +
        "because catalogue release comparisons depend on accurate dates.")]
    public void Simple_episode_valid_release_date_is_parsed()
    {
        // Arrange
        var releaseDate = DomainTestFixture.UtcDateDaysAgo(5).ToString("yyyy-MM-dd");
        var episode = CreateSimpleEpisode(releaseDate);

        // Act
        var result = episode.GetReleaseDate();

        // Assert
        result.Should().Be(DateTime.ParseExact(releaseDate, "yyyy-MM-dd", null));
    }

    [Theory(DisplayName =
        "When FullEpisode ReleaseDate is null or the Spotify placeholder year, GetReleaseDate returns MinValue " +
        "because full and simple episode paths must tolerate the same API quirks.")]
    [InlineData(null)]
    [InlineData("0000")]
    public void Full_episode_invalid_release_date_returns_min_value(string? releaseDate)
    {
        // Arrange
        var episode = CreateFullEpisode(releaseDate);

        // Act
        var result = episode.GetReleaseDate();

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [Fact(DisplayName =
        "When FullEpisode ReleaseDate is a valid Spotify date string, GetReleaseDate parses it " +
        "because enricher and resolver paths share the same release-date semantics.")]
    public void Full_episode_valid_release_date_is_parsed()
    {
        // Arrange
        var releaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd");
        var episode = CreateFullEpisode(releaseDate);

        // Act
        var result = episode.GetReleaseDate();

        // Assert
        result.Should().Be(DateTime.ParseExact(releaseDate, "yyyy-MM-dd", null));
    }

    private SimpleEpisode CreateSimpleEpisode(string? releaseDate) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = releaseDate!
        };

    private FullEpisode CreateFullEpisode(string? releaseDate) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = releaseDate!
        };
}
