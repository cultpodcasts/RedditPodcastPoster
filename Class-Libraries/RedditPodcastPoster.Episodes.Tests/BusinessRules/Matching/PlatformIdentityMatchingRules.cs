using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly EpisodeMerger _merger = new(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));

    [Fact(DisplayName =
        "When a listener submitted an episode via Spotify URL before Spotify assigned an ID, " +
        "indexing must merge the catalogue episode onto that stored row — not create a duplicate, " +
        "even if the Reddit title differs from the Spotify title.")]
    public void Submitted_via_Spotify_URL_before_ID_exists_merges_on_reindex()
    {
        // Given a podcast episode stored from a URL submission (Spotify URL, no Spotify ID)
        var podcast = PodcastFixtures.Standard(id: Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2"));
        var release = DateTime.UtcNow.Date;
        var stored = EpisodeFixtures.SubmittedViaSpotifyUrlOnly(
            SpotifyUrl,
            title: "Reddit post title",
            release: release);
        var expected = EpisodeExpectation.From(stored);

        // When Spotify catalogue returns the same URL with a catalogue title and ID
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Spotify catalogue title",
            SpotifyUrl,
            release,
            TimeSpan.FromMinutes(45));

        // Then indexing merges onto the stored episode and fills the Spotify ID
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

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
        // Given a stored episode with a Spotify ID
        var podcast = PodcastFixtures.Standard();
        var release = DateTime.UtcNow.AddMonths(-6);
        var stored = EpisodeFixtures.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Stored title",
            SpotifyUrl,
            release,
            TimeSpan.FromMinutes(45));
        var expected = EpisodeExpectation.From(stored);

        // When Spotify catalogue returns the same ID with updated metadata
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Incoming title",
            SpotifyUrl,
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45),
            description: "Incoming description");

        // Then indexing merges onto the stored episode without adding a duplicate
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

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
        // Given two episodes with different Spotify IDs but the same title
        var podcast = PodcastFixtures.Standard();
        var existing = EpisodeFixtures.FromSpotifyCatalogue(
            "different-id",
            "Shared title",
            SpotifyUrl,
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Shared title",
            new Uri($"https://open.spotify.com/episode/{SpotifyEpisodeId}"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));

        // When indexing attempts to merge the discovered episode
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Then a new episode is added rather than merged
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When two episodes share the same YouTube video ID, " +
        "indexing must merge them — even when titles differ.")]
    public void Same_YouTube_video_ID_merges_onto_existing_episode()
    {
        // Given a stored episode with a YouTube video ID
        const string youTubeId = "l_iHjZWIsXw";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var release = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var podcast = PodcastFixtures.Standard();
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Stored YouTube title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            YouTubeId = youTubeId,
            Urls = new ServiceUrls { YouTube = youTubeUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        // When YouTube returns the same video ID with a different title
        var discovered = EpisodeFixtures.FromYouTubeVideo(
            youTubeId,
            "Incoming YouTube title",
            release,
            TimeSpan.FromMinutes(45));

        // Then indexing recognizes the same row — no duplicate added
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

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
        // Given two episodes with different YouTube IDs but the same title
        const string sharedTitle = "Shared title";
        var release = DateTime.UtcNow;
        var length = TimeSpan.FromMinutes(45);
        var podcast = PodcastFixtures.Standard();
        var existing = EpisodeFixtures.FromYouTubeVideo(
            "video-id-one",
            sharedTitle,
            release,
            length);
        var discovered = EpisodeFixtures.FromYouTubeVideo(
            "video-id-two",
            sharedTitle,
            release,
            length);

        // When indexing attempts to merge the discovered episode
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Then a new episode is added rather than merged
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }
}
