using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Extensions;

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

    [Theory(DisplayName =
        "When SimpleEpisode IsPlayable is set, IsSpotifyFree mirrors that flag " +
        "because paywall filtering uses IsPlayable as the free-episode signal.")]
    [InlineData(true)]
    [InlineData(false)]
    public void Simple_episode_is_spotify_free_matches_is_playable(bool isPlayable)
    {
        // Arrange
        var episode = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"));
        episode.IsPlayable = isPlayable;

        // Act / Assert
        episode.IsSpotifyFree().Should().Be(isPlayable);
    }

    [Theory(DisplayName =
        "When FullEpisode IsPlayable is set, IsSpotifyFree mirrors that flag " +
        "because enricher and resolver gates share the same free-episode helper.")]
    [InlineData(true)]
    [InlineData(false)]
    public void Full_episode_is_spotify_free_matches_is_playable(bool isPlayable)
    {
        // Arrange
        var episode = CreateFullEpisode(DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"));
        episode.IsPlayable = isPlayable;

        // Act / Assert
        episode.IsSpotifyFree().Should().Be(isPlayable);
    }

    [Fact(DisplayName =
        "When SimpleEpisode is the base SDK type without Restrictions, GetSpotifyRestrictionReason returns (none) " +
        "because absent restriction data must still log an explicit reason placeholder.")]
    public void Simple_episode_without_restrictions_returns_none()
    {
        // Arrange
        var episode = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"));
        episode.IsPlayable = false;

        // Act / Assert
        episode.GetSpotifyRestrictionReason().Should().Be(SpotifyEpisodeExtensions.AbsentRestrictionReason);
    }

    [Fact(DisplayName =
        "When SimpleEpisodeWithRestrictions has restrictions.reason, GetSpotifyRestrictionReason returns that value " +
        "because skip logs should surface why Spotify marked the episode non-playable.")]
    public void Simple_episode_with_restriction_reason_returns_reason()
    {
        // Arrange
        var episode = new SimpleEpisodeWithRestrictions
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string> { ["reason"] = "payment_required" }
        };

        // Act / Assert
        episode.GetSpotifyRestrictionReason().Should().Be("payment_required");
    }

    [Fact(DisplayName =
        "When FullEpisodeWithRestrictions has empty Restrictions, GetSpotifyRestrictionReason returns (none) " +
        "because missing reason must not be logged as a blank value.")]
    public void Full_episode_with_empty_restrictions_returns_none()
    {
        // Arrange
        var episode = new FullEpisodeWithRestrictions
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string>()
        };

        // Act / Assert
        episode.GetSpotifyRestrictionReason().Should().Be(SpotifyEpisodeExtensions.AbsentRestrictionReason);
    }

    [Fact(DisplayName =
        "When FullEpisodeWithRestrictions has restrictions.reason, GetSpotifyRestrictionReason returns that value " +
        "because hydrate/enricher skip paths share the same logging helper.")]
    public void Full_episode_with_restriction_reason_returns_reason()
    {
        // Arrange
        var episode = new FullEpisodeWithRestrictions
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            IsPlayable = false,
            Restrictions = new Dictionary<string, string> { ["reason"] = "payment_required" }
        };

        // Act / Assert
        episode.GetSpotifyRestrictionReason().Should().Be("payment_required");
    }

    private SimpleEpisode CreateSimpleEpisode(string? releaseDate) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = releaseDate!,
            IsPlayable = true
        };

    private FullEpisode CreateFullEpisode(string? releaseDate) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            ReleaseDate = releaseDate!,
            IsPlayable = true
        };
}
