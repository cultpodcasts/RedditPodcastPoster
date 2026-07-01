using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeMergerTests
{
    private static readonly Guid PodcastId = Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2");
    private const string SpotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
    private static readonly Uri SpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}");
    private static readonly Uri SpotifyUrlWithQuery = new($"{SpotifyUrl}?si=abc123");

    private readonly EpisodeMerger _sut = new(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));

    [Fact]
    public void MergeEpisodes_WhenExistingEpisodeCreatedTodayWithSpotifyUrlOnly_DoesNotAddDuplicate()
    {
        // Regression: podcast 4672c845-15b4-4f88-bbff-567d521fe4a2 — episode submitted via Spotify URL
        // when publicly available; YouTube members-first timing does not affect Spotify catalogue dates.
        // Indexing must merge onto the stored episode even when titles differ and SpotifyId is unset.
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var existing = CreateExistingEpisode(
            string.Empty,
            SpotifyUrl,
            release: DateTime.UtcNow.Date,
            title: "Reddit post title");
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Spotify catalogue title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            DateTime.UtcNow.Date,
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty("indexing must merge onto the episode already stored for this Spotify URL");
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(existing.Id);
    }

    [Fact]
    public void MergeEpisodes_WhenExistingEpisodeHasMatchingSpotifyId_DoesNotAddDuplicate()
    {
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Test Podcast" };
        var existing = CreateExistingEpisode(SpotifyEpisodeId, SpotifyUrl, release: DateTime.UtcNow.AddMonths(-6));
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Incoming title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            DateTime.UtcNow,
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        existing.Id.Should().NotBe(incoming.Id);
    }

    [Fact]
    public void MergeEpisodes_WhenExistingEpisodeHasMatchingSpotifyUrlOnly_DoesNotAddDuplicate()
    {
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Test Podcast" };
        var existing = CreateExistingEpisode(string.Empty, SpotifyUrlWithQuery, release: DateTime.UtcNow.AddMonths(-6));
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Incoming title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            DateTime.UtcNow,
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        existing.SpotifyId.Should().Be(SpotifyEpisodeId);
    }

    [Fact]
    public void MergeEpisodes_WhenMembersFirstReleaseDateOlderThanPublicAvailability_MergesAndUpdatesRelease()
    {
        // YouTube members-first: episode was on YouTube earlier; Spotify catalogue release (24th) can
        // differ from when the episode becomes publicly available and indexing returns a newer timestamp.
        var membersFirstRelease = new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
        var publicRelease = new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var existing = CreateExistingEpisode(
            SpotifyEpisodeId,
            SpotifyUrl,
            release: membersFirstRelease,
            title: "Submitted via URL");
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Spotify catalogue title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            publicRelease,
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        existing.Release.Should().Be(publicRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyCatalogueReleasePredatesSubmission_DoesNotAddDuplicate()
    {
        // Spotify API release (e.g. 24th) can precede public/URL-submission day when YouTube had members-only access.
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var existing = CreateExistingEpisode(
            string.Empty,
            SpotifyUrl,
            release: new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            title: "Reddit post title");
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Spotify catalogue title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
    }

    [Fact]
    public void MergeEpisodes_WhenYouTubeTitleDiffersButReleaseAndDurationMatch_MergesYouTubeOntoExisting()
    {
        // Regression: Postmormon Postmortem — YouTube title has typo ("Masscare") vs stored ("Massacre").
        var existingId = Guid.Parse("086b02d5-9ec7-432e-8e57-9279d32374da");
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existingLength = TimeSpan.FromSeconds(878.503);
        var incomingLength = TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39);
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Postmormon Postmortem" };
        var existing = new Episode
        {
            Id = existingId,
            PodcastId = podcast.Id,
            Title = "The Bear River Massacre and the Mormon History Behind Washakie Ward",
            Release = release,
            Length = existingLength,
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var incoming = Episode.FromYouTube(
            "l_iHjZWIsXw",
            "The Bear River Masscare and the Mormon History Behind the Washakie Ward",
            "YouTube description",
            incomingLength,
            false,
            release,
            new Uri("https://www.youtube.com/watch?v=l_iHjZWIsXw"),
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(existingId);
        existing.YouTubeId.Should().Be("l_iHjZWIsXw");
        existing.Urls.YouTube!.ToString().Should().Contain("l_iHjZWIsXw");
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyIdsDiffer_DoesNotMatchByTitle()
    {
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Test Podcast" };
        var existing = CreateExistingEpisode("different-id", SpotifyUrl, release: DateTime.UtcNow);
        existing.Title = "Shared title";
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Shared title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            DateTime.UtcNow,
            new Uri($"https://open.spotify.com/episode/{SpotifyEpisodeId}"),
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }

    private static Episode CreateExistingEpisode(string spotifyId, Uri spotifyUrl, DateTime release, string title = "Existing title")
    {
        return new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = PodcastId,
            Title = title,
            Release = release,
            SpotifyId = spotifyId,
            Urls = new ServiceUrls { Spotify = spotifyUrl }
        };
    }
}
