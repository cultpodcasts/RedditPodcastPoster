using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Merging;

/// <summary>
/// Field-level merge rules characterize current EpisodeMerger fill-missing behaviour before domain extraction.
/// </summary>
public class EpisodeMergingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "Merge fills missing Spotify URLs; it does not replace an existing Spotify URL.")]
    public void Merge_fills_missing_Spotify_URL_without_replacing_existing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var existingSpotifyUrl = spotifyInput.SpotifyUrl;
        var incomingSpotifyUrl = new Uri($"{existingSpotifyUrl}?si=incoming");
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithSpotify(spotifyInput.SpotifyId, existingSpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithSpotifyUrl(incomingSpotifyUrl));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing platform IDs; it does not overwrite an existing ID with a different one.")]
    public void Merge_fills_missing_SpotifyId_without_overwriting_different_existing_ID()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when incoming carries no Spotify ID to fill");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Spotify IDs when the stored episode has none.")]
    public void Merge_fills_missing_SpotifyId_on_YouTube_matched_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithYouTube(youTubeInput.YouTubeId)
            .Create();
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);

        var discovered = _fixture.BuildEpisode()
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .WithYouTube(youTubeInput.YouTubeId)
            .Create();

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge may replace a truncated description (ending in ...) with a longer description; " +
        "it does not replace a complete description with a shorter one.")]
    public void Merge_extends_truncated_description_ending_in_ellipsis()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        const string truncatedDescription = "This is a short preview...";
        const string fullDescription = "This is a short preview with the complete episode summary and details.";
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithDescription(truncatedDescription)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored).WithDescription(fullDescription);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithDescription(fullDescription));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "A discovered episode with no match is added as a new row with a new ID.")]
    public void No_match_adds_new_episode_with_new_Id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateSpotifyCatalogueEpisode();
        var discovered = _fixture.CreateSpotifyCatalogueEpisode();

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        var added = result.AddedEpisodes.Single();
        added.Id.Should().NotBe(stored.Id);
        added.Id.Should().NotBe(Guid.Empty);
        added.SpotifyId.Should().Be(discovered.SpotifyId);
    }

    [Fact(DisplayName =
        "Merge fills missing artwork per platform.")]
    public void Merge_fills_missing_YouTube_artwork()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var incomingImage = _fixture.DefaultYouTubeImage(youTubeInput.YouTubeId);
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl, incomingImage);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithImage(incomingImage));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing artwork per platform; it does not replace existing artwork.")]
    public void Merge_does_not_replace_existing_Spotify_artwork()
    {
        // Arrange
        var existingImage = new Uri("https://i.scdn.co/image/existing-spotify-artwork");
        var incomingImage = new Uri("https://i.scdn.co/image/incoming-spotify-artwork");
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .WithSpotifyImage(existingImage)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithDescription("Incoming description")
            .WithImage(incomingImage));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when artwork already present");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge does not replace a complete description with a shorter one.")]
    public void Merge_does_not_replace_complete_description_with_shorter_text()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        const string completeDescription =
            "This is a complete episode summary with full details about the topic and guests.";
        const string shorterDescription = "This is a complete episode summary.";
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithDescription(completeDescription)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithDescription(shorterDescription));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("complete descriptions must not be shortened on merge");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Apple URLs; it does not replace an existing Apple URL.")]
    public void Merge_fills_missing_Apple_URL_on_Apple_matched_episode()
    {
        // Arrange
        var appleInput = _fixture.CreateAppleCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithApple(appleInput.AppleId)
            .Create();
        var expected = EpisodeExpectation.From(stored)
            .WithApple(appleInput.AppleId, appleInput.AppleUrl);

        var discovered = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithAppleId(appleInput.AppleId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.")]
    public void Merge_fills_missing_YouTube_URL_on_YouTube_matched_episode()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithYouTube(youTubeInput.YouTubeId)
            .Create();
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Apple URLs; it does not replace an existing Apple URL.")]
    public void Merge_does_not_replace_existing_Apple_URL()
    {
        // Arrange
        var appleInput = _fixture.CreateAppleCatalogueInput();
        var existingAppleUrl = appleInput.AppleUrl;
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithApple(appleInput.AppleId, existingAppleUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithAppleId(appleInput.AppleId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.")]
    public void Merge_does_not_replace_existing_YouTube_URL()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var existingYouTubeUrl = youTubeInput.YouTubeUrl;
        var incomingYouTubeUrl = new Uri($"https://youtu.be/{youTubeInput.YouTubeId}");
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithYouTube(youTubeInput.YouTubeId, existingYouTubeUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithYouTubeUrl(incomingYouTubeUrl));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Spotify catalogue release is date-only: re-indexing must not overwrite a stored catalogue release " +
        "with a newer public availability date.")]
    public void Spotify_reindex_preserves_stored_catalogue_release()
    {
        // Arrange
        const int publicAvailabilityDaysAfterCatalogue = 4;
        var catalogueRelease = DomainTestFixture.UtcDateDaysAgo(10);
        var publicRelease = catalogueRelease.AddDays(publicAvailabilityDaysAfterCatalogue).AddHours(12);
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b.WithRelease(catalogueRelease));
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(_fixture.Create<string>())
            .WithRelease(catalogueRelease)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(publicRelease));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("Spotify re-index must not bump release when catalogue date is newer");
        stored.ShouldMatchExpectation(expected);
    }
}
