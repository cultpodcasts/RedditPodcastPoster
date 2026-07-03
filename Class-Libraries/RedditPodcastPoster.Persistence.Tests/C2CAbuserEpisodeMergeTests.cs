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
    private static readonly Guid ExistingId = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362");
    private const string SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";

    private static Podcast CreatePodcast() => new()
    {
        Id = Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200"),
        Name = "Cults to Consciousness",
        ReleaseAuthority = Service.YouTube,
        YouTubePublicationOffset = C2CDelayTicks,
        SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH"
    };

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EpisodesReleaseMatch_WhenSpotifyReleaseVaries(int spotifyReleaseOffsetDaysFromCatalogue)
    {
        var podcast = CreatePodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserYouTubeRelease;
        var spotifyRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease
            .AddDays(spotifyReleaseOffsetDaysFromCatalogue);
        var length = DomainTestFixture.Incidents.C2CAbuserYouTubeLength;

        var existing = new Episode
        {
            Id = ExistingId,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = length,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            length,
            false,
            spotifyRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var releaseMatches = EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existing, incoming);
        var matcher = EpisodeDomainTestServices.CreateMatcher();
        var isMatch = matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Diagnostic output for investigation
        releaseMatches.Should().BeTrue($"Spotify release {spotifyRelease:O} should align after delay adjustment");
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyIncomingMatchesYouTubeOnlyEpisode_MergesOntoExisting()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserYouTubeRelease;
        var spotifyRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease;
        var youTubeLength = DomainTestFixture.Incidents.C2CAbuserYouTubeLength;
        var spotifyLength = DomainTestFixture.Incidents.C2CAbuserSpotifyLength;

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = youTubeLength,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            spotifyLength,
            false,
            spotifyRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var sut = EpisodeDomainTestServices.CreateMerger();
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(ExistingId);
        existing.SpotifyId.Should().Be(SpotifyId);
        existing.Release.Should().Be(youTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyReindexMatchesExistingSpotifyId_PreservesYouTubeReleaseDate()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserYouTubeRelease;
        var spotifyCatalogueRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease;

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = DomainTestFixture.Incidents.C2CAbuserYouTubeLength,
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = SpotifyId,
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri($"https://open.spotify.com/episode/{SpotifyId}")
            }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            DomainTestFixture.Incidents.C2CAbuserSpotifyLength,
            false,
            spotifyCatalogueRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var sut = EpisodeDomainTestServices.CreateMerger();
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        existing.Release.Should().Be(youTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyOnlyReindexSameDateWithTime_DoesNotBackfillYouTubeReleaseTime()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease;
        var spotifyRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease;

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = DomainTestFixture.Incidents.C2CAbuserYouTubeLength,
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = SpotifyId,
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri($"https://open.spotify.com/episode/{SpotifyId}")
            }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            DomainTestFixture.Incidents.C2CAbuserSpotifyLength,
            false,
            spotifyRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);
        // Episode model may retain API time before normalization; merge must still treat Spotify as date-only.
        incoming.Release = spotifyRelease.AddHours(8);

        var sut = EpisodeDomainTestServices.CreateMerger();
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().BeEmpty("Spotify-only merge must not backfill time on same date");
        existing.Release.Should().Be(youTubeRelease);
    }
}
