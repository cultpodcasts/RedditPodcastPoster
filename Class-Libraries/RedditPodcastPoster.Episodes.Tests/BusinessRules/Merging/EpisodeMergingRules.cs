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

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "Merge fills missing Spotify URLs; it does not replace an existing Spotify URL.")]
    public void Merge_fills_missing_Spotify_URL_without_replacing_existing()
    {
        // Given a stored episode with a Spotify URL already set
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = _fixture.CreateEpisode(e =>
        {
            e.PodcastId = podcast.Id;
            e.Title = "Episode title";
            e.Release = release;
            e.Length = TimeSpan.FromMinutes(45);
            e.SpotifyId = SpotifyEpisodeId;
            e.Urls = new ServiceUrls { Spotify = ExistingSpotifyUrl };
        });
        var expected = EpisodeExpectation.From(stored);

        // When Spotify re-index returns the same ID with a different URL variant
        var discovered = _fixture.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Episode title",
            IncomingSpotifyUrl,
            release,
            TimeSpan.FromMinutes(45));

        // Then the stored Spotify URL is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing platform IDs; it does not overwrite an existing ID with a different one.")]
    public void Merge_fills_missing_SpotifyId_without_overwriting_different_existing_ID()
    {
        // Given a stored episode with a Spotify ID and YouTube video already assigned
        const string existingSpotifyId = "existingSpotifyId01";
        const string youTubeId = "sharedYouTubeId01";
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = existingSpotifyId,
            YouTubeId = youTubeId,
            Urls = new ServiceUrls
            {
                Spotify = new Uri($"https://open.spotify.com/episode/{existingSpotifyId}"),
                YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}")
            }
        };
        var expected = EpisodeExpectation.From(stored);

        // When YouTube re-index returns the same video without a Spotify ID to fill
        var discovered = _fixture.FromYouTubeVideo(
            youTubeId,
            "Episode title",
            release,
            TimeSpan.FromMinutes(45));

        // Then merge matches on YouTube ID but preserves the existing Spotify ID
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when incoming carries no Spotify ID to fill");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Spotify IDs when the stored episode has none.")]
    public void Merge_fills_missing_SpotifyId_on_YouTube_matched_episode()
    {
        // Given a stored episode with YouTube identity but no Spotify ID
        const string youTubeId = "sharedYouTubeId02";
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            YouTubeId = youTubeId,
            Urls = new ServiceUrls { YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}") }
        };
        var expected = EpisodeExpectation.From(stored).WithSpotify(SpotifyEpisodeId, ExistingSpotifyUrl);

        // When Spotify returns the same episode via shared YouTube identity metadata
        var discovered = new Episode
        {
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            YouTubeId = youTubeId,
            Urls = new ServiceUrls
            {
                Spotify = ExistingSpotifyUrl,
                YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}")
            }
        };

        // Then merge fills the missing Spotify ID without adding a duplicate
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge may replace a truncated description (ending in ...) with a longer description; " +
        "it does not replace a complete description with a shorter one.")]
    public void Merge_extends_truncated_description_ending_in_ellipsis()
    {
        // Given a stored episode with a truncated description
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        const string truncatedDescription = "This is a short preview...";
        const string fullDescription = "This is a short preview with the complete episode summary and details.";
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Description = truncatedDescription,
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = ExistingSpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored).WithDescription(fullDescription);

        // When Spotify re-index returns a longer description for the same episode
        var discovered = _fixture.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Episode title",
            ExistingSpotifyUrl,
            release,
            TimeSpan.FromMinutes(45),
            description: fullDescription);

        // Then merge replaces the truncated description with the full text
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "A discovered episode with no match is added as a new row with a new ID.")]
    public void No_match_adds_new_episode_with_new_Id()
    {
        // Given a stored episode with no overlapping platform identity
        var podcast = _fixture.StandardPodcast();
        var stored = _fixture.FromSpotifyCatalogue(
            "storedSpotifyId01",
            "Stored episode title",
            new Uri("https://open.spotify.com/episode/storedSpotifyId01"),
            DateTime.UtcNow.AddMonths(-2),
            TimeSpan.FromMinutes(30));

        // When indexing discovers an unrelated episode
        var discovered = _fixture.FromSpotifyCatalogue(
            "newSpotifyId000001",
            "Brand new episode title",
            new Uri("https://open.spotify.com/episode/newSpotifyId000001"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(50));

        // Then a new episode row is added with a distinct ID
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        var added = result.AddedEpisodes.Single();
        added.Id.Should().NotBe(stored.Id);
        added.Id.Should().NotBe(Guid.Empty);
        added.SpotifyId.Should().Be("newSpotifyId000001");
    }

    [Fact(DisplayName =
        "Merge fills missing artwork per platform.")]
    public void Merge_fills_missing_YouTube_artwork()
    {
        // Given a stored episode with YouTube identity but no artwork
        const string youTubeId = "artworkYouTubeId01";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var incomingImage = new Uri("https://i.ytimg.com/vi/artworkYouTubeId01/maxresdefault.jpg");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            YouTubeId = youTubeId,
            Urls = new ServiceUrls { YouTube = youTubeUrl }
        };
        var expected = EpisodeExpectation.From(stored).WithYouTube(youTubeId, youTubeUrl, incomingImage);

        // When YouTube re-index returns artwork for the same video
        var discovered = Episode.FromYouTube(
            youTubeId,
            "Episode title",
            "YouTube description",
            TimeSpan.FromMinutes(45),
            false,
            release,
            youTubeUrl,
            incomingImage);

        // Then merge fills the missing YouTube artwork without adding a duplicate
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing artwork per platform; it does not replace existing artwork.")]
    public void Merge_does_not_replace_existing_Spotify_artwork()
    {
        // Given a stored episode with Spotify artwork already set
        var existingImage = new Uri("https://i.scdn.co/image/existing-spotify-artwork");
        var incomingImage = new Uri("https://i.scdn.co/image/incoming-spotify-artwork");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = ExistingSpotifyUrl },
            Images = new EpisodeImages { Spotify = existingImage }
        };
        var expected = EpisodeExpectation.From(stored);

        // When Spotify re-index returns different artwork for the same episode
        var discovered = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Episode title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            release,
            ExistingSpotifyUrl,
            incomingImage);

        // Then the stored Spotify artwork is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when artwork already present");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge does not replace a complete description with a shorter one.")]
    public void Merge_does_not_replace_complete_description_with_shorter_text()
    {
        // Given a stored episode with a complete description
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        const string completeDescription =
            "This is a complete episode summary with full details about the topic and guests.";
        const string shorterDescription = "This is a complete episode summary.";
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Description = completeDescription,
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = ExistingSpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        // When Spotify re-index returns a shorter description for the same episode
        var discovered = _fixture.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Episode title",
            ExistingSpotifyUrl,
            release,
            TimeSpan.FromMinutes(45),
            description: shorterDescription);

        // Then the stored complete description is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("complete descriptions must not be shortened on merge");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Apple URLs; it does not replace an existing Apple URL.")]
    public void Merge_fills_missing_Apple_URL_on_Apple_matched_episode()
    {
        // Given a stored episode with Apple identity but no Apple URL
        const long appleId = 1635013493;
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            AppleId = appleId
        };
        var expected = EpisodeExpectation.From(stored).WithApple(appleId, appleUrl);

        // When Apple re-index returns the same episode with a URL
        var discovered = _fixture.FromAppleEpisode(
            appleId,
            "Episode title",
            release,
            TimeSpan.FromMinutes(45));

        // Then merge fills the missing Apple URL without adding a duplicate
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.")]
    public void Merge_fills_missing_YouTube_URL_on_YouTube_matched_episode()
    {
        // Given a stored episode with YouTube identity but no YouTube URL
        const string youTubeId = "fillMissingYouTube1";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            YouTubeId = youTubeId
        };
        var expected = EpisodeExpectation.From(stored).WithYouTube(youTubeId, youTubeUrl);

        // When YouTube re-index returns the same video with a URL
        var discovered = _fixture.FromYouTubeVideo(
            youTubeId,
            "Episode title",
            release,
            TimeSpan.FromMinutes(45));

        // Then merge fills the missing YouTube URL without adding a duplicate
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing Apple URLs; it does not replace an existing Apple URL.")]
    public void Merge_does_not_replace_existing_Apple_URL()
    {
        // Given a stored episode with an Apple URL already set
        const long appleId = 1635013492;
        var existingAppleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var incomingAppleUrl = new Uri($"https://podcasts.apple.com/gb/podcast/episode/id{appleId}");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            AppleId = appleId,
            Urls = new ServiceUrls { Apple = existingAppleUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        // When Apple re-index returns the same ID with a different URL variant
        var discovered = _fixture.FromAppleEpisode(
            appleId,
            "Episode title",
            release,
            TimeSpan.FromMinutes(45));

        // Then the stored Apple URL is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Merge fills missing YouTube URLs; it does not replace an existing YouTube URL.")]
    public void Merge_does_not_replace_existing_YouTube_URL()
    {
        // Given a stored episode with a YouTube URL already set
        const string youTubeId = "existingYouTubeId1";
        var existingYouTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var incomingYouTubeUrl = new Uri($"https://youtu.be/{youTubeId}");
        var podcast = _fixture.StandardPodcast();
        var release = DateTime.UtcNow.AddMonths(-1);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = release,
            Length = TimeSpan.FromMinutes(45),
            YouTubeId = youTubeId,
            Urls = new ServiceUrls { YouTube = existingYouTubeUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        // When YouTube re-index returns the same video with a different URL variant
        var discovered = Episode.FromYouTube(
            youTubeId,
            "Episode title",
            "YouTube description",
            TimeSpan.FromMinutes(45),
            false,
            release,
            incomingYouTubeUrl,
            null);

        // Then the stored YouTube URL is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Spotify catalogue release is date-only: re-indexing must not overwrite a stored catalogue release " +
        "with a newer public availability date.")]
    public void Spotify_reindex_preserves_stored_catalogue_release()
    {
        // Given a stored episode indexed with an earlier Spotify catalogue release date
        var catalogueRelease = new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
        var publicRelease = new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);
        var podcast = _fixture.StandardPodcast(id: Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2"));
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Submitted via URL",
            Release = catalogueRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = ExistingSpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        // When Spotify re-index returns a newer public availability date for the same Spotify ID
        var discovered = _fixture.FromSpotifyCatalogue(
            SpotifyEpisodeId,
            "Spotify catalogue title",
            ExistingSpotifyUrl,
            publicRelease,
            TimeSpan.FromMinutes(45),
            description: "Incoming description");

        // Then the stored catalogue release is preserved
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("Spotify re-index must not bump release when catalogue date is newer");
        stored.ShouldMatchExpectation(expected);
    }
}
