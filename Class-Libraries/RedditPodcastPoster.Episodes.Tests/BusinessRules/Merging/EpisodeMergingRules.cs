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
    private const string SpotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
    private static readonly Uri ExistingSpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}");
    private static readonly Uri IncomingSpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}?si=incoming");
    private static readonly DateTime SharedRelease = DomainTestFixture.UtcDaysAgo(30);

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "Merge fills missing Spotify URLs; it does not replace an existing Spotify URL.")]
    public void Merge_fills_missing_Spotify_URL_without_replacing_existing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: IncomingSpotifyUrl,
            release: SharedRelease);

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
        const string existingSpotifyId = "5nT8vW2xY4zA6bC8dE0fG2";
        const string youTubeId = "dQw4w9WgXcQ";
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithSpotify(existingSpotifyId, _fixture.DefaultSpotifyUrl(existingSpotifyId))
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeId)
            .WithRelease(SharedRelease));

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
        const string youTubeId = "9aBcDeFgHiJ";
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithYouTube(youTubeId)
            .Create();
        var expected = EpisodeExpectation.From(stored).WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl);

        var discovered = _fixture.BuildEpisode()
            .WithRelease(SharedRelease)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .WithYouTube(youTubeId)
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
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithDescription(truncatedDescription)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored).WithDescription(fullDescription);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: ExistingSpotifyUrl,
            release: SharedRelease,
            description: fullDescription);

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
        var stored = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithSpotifyId("6O1Z1s7ca0PI8Gq1rdt3j4"));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b.WithSpotifyId("3vKvHj9mNoPqRsTuVwXyZ1"));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        var added = result.AddedEpisodes.Single();
        added.Id.Should().NotBe(stored.Id);
        added.Id.Should().NotBe(Guid.Empty);
        added.SpotifyId.Should().Be("3vKvHj9mNoPqRsTuVwXyZ1");
    }

    [Fact(DisplayName =
        "Merge fills missing artwork per platform.")]
    public void Merge_fills_missing_YouTube_artwork()
    {
        // Arrange
        const string youTubeId = "kLmNoPqRsTu";
        var incomingImage = _fixture.DefaultYouTubeImage(youTubeId);
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();
        var expected = EpisodeExpectation.From(stored).WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId), incomingImage);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeId)
            .WithRelease(SharedRelease)
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
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .WithSpotifyImage(existingImage)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(SpotifyEpisodeId)
            .WithSpotifyUrl(ExistingSpotifyUrl)
            .WithRelease(SharedRelease)
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
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithDescription(completeDescription)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            spotifyUrl: ExistingSpotifyUrl,
            release: SharedRelease,
            description: shorterDescription);

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
        const long appleId = 1635013493;
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithApple(appleId)
            .Create();
        var expected = EpisodeExpectation.From(stored).WithApple(appleId, _fixture.DefaultAppleUrl(appleId));

        var discovered = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithAppleId(appleId)
            .WithRelease(SharedRelease));

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
        const string youTubeId = "xYzAbCdEfGh";
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithYouTube(youTubeId)
            .Create();
        var expected = EpisodeExpectation.From(stored).WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeId)
            .WithRelease(SharedRelease));

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
        const long appleId = 1635013492;
        var existingAppleUrl = _fixture.DefaultAppleUrl(appleId);
        var incomingAppleUrl = new Uri($"https://podcasts.apple.com/gb/podcast/episode/id{appleId}");
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithApple(appleId, existingAppleUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithAppleId(appleId)
            .WithRelease(SharedRelease));

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
        const string youTubeId = "mNpQrStUvWx";
        var existingYouTubeUrl = _fixture.DefaultYouTubeUrl(youTubeId);
        var incomingYouTubeUrl = new Uri($"https://youtu.be/{youTubeId}");
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(SharedRelease)
            .WithYouTube(youTubeId, existingYouTubeUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeId)
            .WithRelease(SharedRelease)
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
        var catalogueRelease = DomainTestFixture.Incidents.OtoIncomingSpotifyRelease;
        var publicRelease = catalogueRelease.AddDays(4).AddHours(12);
        var podcast = _fixture.CreatePodcast(p => p.Id = Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2"));
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle("Submitted via URL")
            .WithRelease(catalogueRelease)
            .WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl)
            .Create();
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(SpotifyEpisodeId)
            .WithSpotifyUrl(ExistingSpotifyUrl)
            .WithRelease(publicRelease));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("Spotify re-index must not bump release when catalogue date is newer");
        stored.ShouldMatchExpectation(expected);
    }
}
