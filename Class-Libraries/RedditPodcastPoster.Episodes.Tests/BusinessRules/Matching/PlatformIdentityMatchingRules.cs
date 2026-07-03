using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// First business-rule tests. These characterize current merge/match behaviour before domain extraction.
/// </summary>
public class PlatformIdentityMatchingRules
{
    private static readonly Uri SpotifyUrl = new("https://open.spotify.com/episode/1UncRhHtmojlTq2mO0Gntz");
    private const string SpotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When a listener submitted an episode via Spotify URL before Spotify assigned an ID, " +
        "indexing must merge the catalogue episode onto that stored row — not create a duplicate, " +
        "even if the Reddit title differs from the Spotify title.")]
    public void Submitted_via_Spotify_URL_before_ID_exists_merges_on_reindex()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Id = Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2"));
        var release = DateTime.UtcNow.Date;
        var stored = _fixture.CreateSubmittedViaSpotifyUrlOnly(
            SpotifyUrl,
            title: "Reddit post title",
            release: release);
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: SpotifyUrl,
            release: release);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(SpotifyEpisodeId, SpotifyUrl));
    }

    [Fact(DisplayName =
        "When two stored episodes already share the same Spotify ID, " +
        "indexing must treat the catalogue episode as the same row — not create a duplicate.")]
    public void Same_Spotify_ID_merges_onto_existing_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var release = DateTime.UtcNow.AddMonths(-6);
        var stored = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: SpotifyUrl,
            release: release);
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: SpotifyUrl,
            release: DateTime.UtcNow);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when only metadata differs");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When two stored episodes already have different Spotify IDs, " +
        "a new Spotify episode must not be merged by title similarity alone.")]
    public void Different_Spotify_IDs_never_merge_by_title()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var existing = _fixture.CreateSpotifyCatalogueEpisode(
            "different-id",
            "Shared title",
            SpotifyUrl,
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            "Shared title",
            new Uri($"https://open.spotify.com/episode/{SpotifyEpisodeId}"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));

        // Act
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Assert
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When two episodes share the same YouTube video ID, " +
        "indexing must merge them — even when titles differ.")]
    public void Same_YouTube_video_ID_merges_onto_existing_episode()
    {
        // Arrange
        const string youTubeId = "l_iHjZWIsXw";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var release = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(release)
            .WithTitle("Stored YouTube title")
            .WithYouTube(youTubeId, youTubeUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(youTubeId, release: release);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when YouTube identity already complete");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When two episodes have different YouTube video IDs, " +
        "they must never merge — even when titles are identical.")]
    public void Different_YouTube_video_IDs_never_merge_by_title()
    {
        // Arrange
        const string sharedTitle = "Shared title";
        var release = DateTime.UtcNow;
        var podcast = _fixture.CreatePodcast();
        var existing = _fixture.CreateYouTubeCatalogueEpisode("video-id-one", title: sharedTitle, release: release);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode("video-id-two", title: sharedTitle, release: release);

        // Act
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Assert
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When two episodes share the same Apple episode ID, " +
        "indexing must merge them — even when titles differ.")]
    public void Same_Apple_ID_merges_onto_existing_episode()
    {
        // Arrange
        const long appleId = 1635013492;
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var release = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(release)
            .WithTitle("Stored Apple title")
            .WithApple(appleId, appleUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateAppleCatalogueEpisode(appleId, release: release);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Apple identity already complete");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When an incoming platform ID is already assigned to a different stored episode, " +
        "indexing must not merge onto the wrong row.")]
    public void Incoming_Spotify_ID_owned_by_another_row_does_not_merge_onto_wrong_candidate()
    {
        // Arrange
        var podcast = _fixture.CreateCultsToConsciousnessPodcast();
        const string otoSpotifyId = "16LveQifI6eBwDXAINpd7G";
        var otoSpotifyUrl = new Uri($"https://open.spotify.com/episode/{otoSpotifyId}");
        var correctOwnerId = Guid.Parse("1c804814-12ac-40c8-a223-88ab7c703d38");
        var wrongYouTubeOnlyId = Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160");
        var correctOwner = new Episode
        {
            Id = correctOwnerId,
            PodcastId = podcast.Id,
            Title = "What Really Happens During \"Ordo Templi Orientis\" Initiations?  (Trapped in a Secret Society)",
            Release = new DateTime(2026, 5, 20, 22, 15, 16, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(42),
            SpotifyId = otoSpotifyId,
            YouTubeId = "l3aIdJeg0vE",
            Urls = new ServiceUrls
            {
                Spotify = otoSpotifyUrl,
                YouTube = new Uri("https://www.youtube.com/watch?v=l3aIdJeg0vE")
            }
        };
        var wrongYouTubeOnly = new Episode
        {
            Id = wrongYouTubeOnlyId,
            PodcastId = podcast.Id,
            Title = "Why He Thinks Daughters Should Parent Their Siblings  (ft. Tia Levings)",
            Release = new DateTime(2026, 5, 31, 21, 15, 27, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35),
            YouTubeId = "u6ZF-2sWQQc",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=u6ZF-2sWQQc") }
        };
        var expectedOwner = EpisodeExpectation.From(correctOwner);
        var expectedWrongRow = EpisodeExpectation.From(wrongYouTubeOnly);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            otoSpotifyId,
            spotifyUrl: otoSpotifyUrl,
            release: new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            length: TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(42));

        // Act
        var result = _merger.MergeEpisodes(podcast, [correctOwner, wrongYouTubeOnly], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("Spotify re-index must not rewrite YouTube release when catalogue date is newer");
        correctOwner.ShouldMatchExpectation(expectedOwner);
        wrongYouTubeOnly.ShouldMatchExpectation(expectedWrongRow);
    }

    [Fact(DisplayName =
        "When two episodes have different Apple episode IDs, " +
        "they must never merge — even when titles are identical.")]
    public void Different_Apple_IDs_never_merge_by_title()
    {
        // Arrange
        const string sharedTitle = "Shared title";
        var release = DateTime.UtcNow;
        var podcast = _fixture.CreatePodcast();
        var existing = _fixture.CreateAppleCatalogueEpisode(1111111111, title: sharedTitle, release: release);
        var discovered = _fixture.CreateAppleCatalogueEpisode(2222222222, title: sharedTitle, release: release);

        // Act
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Assert
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }
}
