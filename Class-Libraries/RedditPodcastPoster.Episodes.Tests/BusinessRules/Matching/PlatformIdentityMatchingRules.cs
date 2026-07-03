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
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When a listener submitted an episode via Spotify URL before Spotify assigned an ID, " +
        "indexing must merge the catalogue episode onto that stored row — not create a duplicate, " +
        "even if the Reddit title differs from the Spotify title.")]
    public void Submitted_via_Spotify_URL_before_ID_exists_merges_on_reindex()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var spotifyUrl = spotifyInput.SpotifyUrl;
        var redditTitle = _fixture.Create<string>();
        var stored = _fixture.CreateSubmittedViaSpotifyUrlOnly(
            spotifyUrl,
            title: redditTitle,
            release: spotifyInput.Release);
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            spotifyInput.SpotifyId,
            spotifyUrl: spotifyUrl);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(spotifyInput.SpotifyId, spotifyUrl));
    }

    [Fact(DisplayName =
        "When two stored episodes already share the same Spotify ID, " +
        "indexing must treat the catalogue episode as the same row — not create a duplicate.")]
    public void Same_Spotify_ID_merges_onto_existing_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateSpotifyCatalogueEpisode();
        var spotifyId = stored.SpotifyId;
        var spotifyUrl = stored.Urls.Spotify!;
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            spotifyId,
            spotifyUrl: spotifyUrl,
            release: DomainTestFixture.UtcToday);

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
        var sharedTitle = _fixture.Create<string>();
        var sharedLength = _fixture.CreateDuration();
        var existing = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(sharedTitle)
            .WithDuration(sharedLength));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(sharedTitle)
            .WithDuration(sharedLength));

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
        var podcast = _fixture.CreatePodcast();
        var discovered = _fixture.CreateYouTubeCatalogueEpisode();
        var youTubeId = discovered.YouTubeId;
        var storedTitle = _fixture.Create<string>();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(discovered.Release)
            .WithTitle(storedTitle)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();
        var expected = EpisodeExpectation.From(stored);

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
        var sharedTitle = _fixture.Create<string>();
        var podcast = _fixture.CreatePodcast();
        var existing = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithTitle(sharedTitle));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithTitle(sharedTitle));

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
        var podcast = _fixture.CreatePodcast();
        var discovered = _fixture.CreateAppleCatalogueEpisode();
        var appleId = discovered.AppleId!.Value;
        var storedTitle = _fixture.Create<string>();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(discovered.Release)
            .WithTitle(storedTitle)
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var expected = EpisodeExpectation.From(stored);

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
        var podcast = _fixture.CreateYouTubeFirstPodcastWithNegativeDelay();
        var spotifyId = _fixture.CreateSpotifyId();
        var correctOwner = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(podcast, spotifyId);
        var wrongYouTubeOnly = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var expectedOwner = EpisodeExpectation.From(correctOwner);
        var expectedWrongRow = EpisodeExpectation.From(wrongYouTubeOnly);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithSpotifyId(spotifyId));

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
        var sharedTitle = _fixture.Create<string>();
        var podcast = _fixture.CreatePodcast();
        var existing = _fixture.CreateAppleCatalogueEpisode(b => b.WithTitle(sharedTitle));
        var discovered = _fixture.CreateAppleCatalogueEpisode(b => b.WithTitle(sharedTitle));

        // Act
        var result = _merger.MergeEpisodes(podcast, [existing], [discovered]);

        // Assert
        result.AddedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Should().BeEmpty();
    }
}
