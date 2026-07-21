using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Persistence.Tests;

public class CrossPlatformYouTubeReleaseAuthorityMergeTests
{
    private readonly DomainTestFixture _fixture = new();

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EpisodesReleaseMatch_WhenSpotifyReleaseVaries(int spotifyReleaseOffsetDaysFromCatalogue)
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (existing, incomingTemplate, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(incomingTemplate.SpotifyId)
            .WithTitle(incomingTemplate.Title)
            .WithSpotifyUrl(incomingTemplate.Urls.Spotify!)
            .WithRelease(incomingTemplate.Release.AddDays(spotifyReleaseOffsetDaysFromCatalogue))
            .WithDuration(existing.Length));

        // Act
        var isMatch = EpisodeDomainTestServices.CreateMatcher()
            .IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Assert
        isMatch.Should().BeTrue(
            $"Spotify release {incoming.Release:O} should align after delay adjustment");
    }

    [Fact(DisplayName =
        "When stored episode has YouTube-only release and Spotify catalogue arrives with aligned calendar date, " +
        "merge attaches Spotify identity and preserves YouTube publish datetime.")]
    public void MergeEpisodes_WhenSpotifyIncomingMatchesYouTubeOnlyEpisode_MergesOntoExisting()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (existing, incoming, spotifyId) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var expectedRelease = existing.Release;

        // Act
        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(existing.Id);
        existing.SpotifyId.Should().Be(spotifyId);
        existing.Release.Should().Be(expectedRelease);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts, re-indexing from Spotify must not replace the YouTube publish datetime " +
        "with a newer Spotify catalogue date.")]
    public void MergeEpisodes_WhenSpotifyReindexMatchesExistingSpotifyId_PreservesYouTubeReleaseDate()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (storedTemplate, incoming, spotifyId) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var existing = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            spotifyId,
            storedTemplate.YouTubeId,
            storedTemplate.Release,
            storedTemplate.Length,
            storedTemplate.Title);

        // Act
        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        existing.Release.Should().Be(storedTemplate.Release);
    }

    [Fact(DisplayName =
        "When stored release is midnight UTC Spotify date and incoming Spotify carries time on the same calendar date, " +
        "merge must not backfill the time — Spotify catalogue release is date-only.")]
    public void MergeEpisodes_WhenSpotifyOnlyReindexSameDateWithTime_DoesNotBackfillYouTubeReleaseTime()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (_, incomingTemplate, spotifyId) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var spotifyDateOnlyRelease = incomingTemplate.Release;
        var existing = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            spotifyId,
            release: spotifyDateOnlyRelease,
            length: incomingTemplate.Length);
        var incoming = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(incomingTemplate.SpotifyId)
            .WithTitle(incomingTemplate.Title)
            .WithSpotifyUrl(incomingTemplate.Urls.Spotify!)
            .WithRelease(spotifyDateOnlyRelease)
            .WithDuration(incomingTemplate.Length));
        incoming.Release = spotifyDateOnlyRelease.AddHours(8);

        // Act
        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty("Spotify-only merge must not backfill time on same date");
        existing.Release.Should().Be(spotifyDateOnlyRelease);
    }
}
