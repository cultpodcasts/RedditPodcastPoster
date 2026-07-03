using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Persistence.Tests;

public class C2CAbuserEpisodeMergeTests
{
    private const long C2CDelayTicks = -27216000000000;
    private readonly DomainTestFixture _fixture = new();

    private static Podcast CreatePodcast() => new()
    {
        Id = DomainTestFixture.Incidents.CultsToConsciousnessPodcastId,
        Name = "Cults to Consciousness",
        ReleaseAuthority = Service.YouTube,
        YouTubePublicationOffset = C2CDelayTicks,
        SpotifyId = DomainTestFixture.Incidents.CultsToConsciousnessSpotifyShowId
    };

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EpisodesReleaseMatch_WhenSpotifyReleaseVaries(int spotifyReleaseOffsetDaysFromCatalogue)
    {
        var podcast = CreatePodcast();
        var existing = _fixture.CreateC2CYouTubeOnlyStoredEpisode(podcast);
        var incoming = _fixture.CreateC2CSpotifyIncoming(
            release: DomainTestFixture.Incidents.C2CAbuserSpotifyRelease
                .AddDays(spotifyReleaseOffsetDaysFromCatalogue),
            length: DomainTestFixture.Incidents.C2CAbuserYouTubeLength);

        var releaseMatches = EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existing, incoming);
        var isMatch = EpisodeDomainTestServices.CreateMatcher()
            .IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        releaseMatches.Should().BeTrue(
            $"Spotify release {incoming.Release:O} should align after delay adjustment");
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyIncomingMatchesYouTubeOnlyEpisode_MergesOntoExisting()
    {
        var podcast = CreatePodcast();
        var existing = _fixture.CreateC2CYouTubeOnlyStoredEpisode(podcast);
        var incoming = _fixture.CreateC2CSpotifyIncoming();

        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(DomainTestFixture.Incidents.C2CAbuserEpisodeId);
        existing.SpotifyId.Should().Be(DomainTestFixture.Incidents.C2CAbuserSpotifyId);
        existing.Release.Should().Be(DomainTestFixture.Incidents.C2CAbuserYouTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyReindexMatchesExistingSpotifyId_PreservesYouTubeReleaseDate()
    {
        var podcast = CreatePodcast();
        var existing = _fixture.CreateC2CYouTubeAuthorityStoredEpisode(
            podcast,
            release: DomainTestFixture.Incidents.C2CAbuserYouTubeRelease,
            length: DomainTestFixture.Incidents.C2CAbuserYouTubeLength);
        var incoming = _fixture.CreateC2CSpotifyIncoming(
            release: DomainTestFixture.Incidents.C2CAbuserSpotifyRelease,
            length: DomainTestFixture.Incidents.C2CAbuserSpotifyLength);

        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        existing.Release.Should().Be(DomainTestFixture.Incidents.C2CAbuserYouTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyOnlyReindexSameDateWithTime_DoesNotBackfillYouTubeReleaseTime()
    {
        var podcast = CreatePodcast();
        var existing = _fixture.CreateC2CYouTubeAuthorityStoredEpisode(
            podcast,
            release: DomainTestFixture.Incidents.C2CAbuserSpotifyRelease,
            length: DomainTestFixture.Incidents.C2CAbuserYouTubeLength);
        var incoming = _fixture.CreateC2CSpotifyIncoming(
            release: DomainTestFixture.Incidents.C2CAbuserSpotifyRelease,
            length: DomainTestFixture.Incidents.C2CAbuserSpotifyLength);
        incoming.Release = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease.AddHours(8);

        var result = EpisodeDomainTestServices.CreateMerger()
            .MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().BeEmpty("Spotify-only merge must not backfill time on same date");
        existing.Release.Should().Be(DomainTestFixture.Incidents.C2CAbuserSpotifyRelease);
    }
}
