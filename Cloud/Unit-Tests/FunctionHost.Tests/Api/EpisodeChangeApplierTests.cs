using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;

namespace FunctionHost.Tests.Api;

/// <summary>
/// Pure-apply unit tests for <see cref="EpisodeChangeApplier"/>: mutates an in-memory
/// <see cref="Episode"/> from an <see cref="EpisodeChangeRequest"/> with no Cosmos or other I/O.
/// Covers the field-by-field apply rules and the <see cref="EpisodeChangeState"/> side-effect flags
/// (UnPost/UnTweet/UnBlueskyPost/UpdatedSubjects/Update*Image/PublishHomepage) that downstream
/// services key off after a curator PATCH.
/// </summary>
public class EpisodeChangeApplierTests
{
    private readonly DomainTestFixture _fixture = new();

    private static EpisodeChangeApplier CreateSut() =>
        new(NullLogger<EpisodeChangeApplier>.Instance);

    private Episode CreateEpisode(Action<Episode>? customize = null)
    {
        var episode = new Episode
        {
            Id = _fixture.CreateGuid(),
            PodcastId = _fixture.CreateGuid(),
            Title = "Original title",
            Description = "Original description",
            Release = DateTime.UtcNow.AddDays(-30),
            Length = TimeSpan.FromMinutes(30),
            Urls = new ServiceUrls()
        };
        customize?.Invoke(episode);
        return episode;
    }

    // ----- Simple scalar fields -----

    [Fact(DisplayName = "Apply sets Title when provided")]
    public void Apply_sets_title()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Title = "New title" });

        // Assert
        episode.Title.Should().Be("New title");
    }

    [Fact(DisplayName = "Apply leaves Title unchanged when whitespace-only")]
    public void Apply_ignores_whitespace_only_title()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Title = "Keep me");
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Title = "   " });

        // Assert
        episode.Title.Should().Be("Keep me");
    }

    [Fact(DisplayName = "Apply sets Description, including clearing to empty string")]
    public void Apply_sets_description_to_empty_string()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Description = "Old description");
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Description = "" });

        // Assert
        episode.Description.Should().Be("");
    }

    [Fact(DisplayName = "Apply parses Duration into Length")]
    public void Apply_parses_duration()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Duration = "01:15:30" });

        // Assert
        episode.Length.Should().Be(new TimeSpan(1, 15, 30));
    }

    [Fact(DisplayName = "Apply sets SearchTerms when provided")]
    public void Apply_sets_search_terms()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { SearchTerms = "cults, documentaries" });

        // Assert
        episode.SearchTerms.Should().Be("cults, documentaries");
    }

    [Fact(DisplayName = "Apply sets HashTag when provided")]
    public void Apply_sets_hash_tag()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { HashTag = " #ABC #XYZ " });

        // Assert
        episode.HashTag.Should().Be("#ABC #XYZ");
    }

    [Fact(DisplayName = "Apply clears HashTag when empty string provided")]
    public void Apply_clears_hash_tag_when_empty()
    {
        // Arrange
        var episode = CreateEpisode(e => e.HashTag = "#Keep");
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { HashTag = "  " });

        // Assert
        episode.HashTag.Should().BeNull();
    }

    [Fact(DisplayName = "Apply sets Explicit/Ignored/Removed booleans when provided")]
    public void Apply_sets_boolean_flags()
    {
        // Arrange
        var episode = CreateEpisode(e =>
        {
            e.Explicit = false;
            e.Ignored = false;
            e.Removed = false;
        });
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Explicit = true,
            Ignored = true,
            Removed = true
        });

        // Assert
        episode.Explicit.Should().BeTrue();
        episode.Ignored.Should().BeTrue();
        episode.Removed.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply sets Language, and clears it when set to empty string")]
    public void Apply_clears_language_on_empty_string()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Language = "en");
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Language = "" });

        // Assert
        episode.Language.Should().BeNull();
    }

    // ----- Release / PublishHomepage -----

    [Fact(DisplayName = "Apply updates Release when provided")]
    public void Apply_sets_release()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var newRelease = DateTime.UtcNow.AddDays(-1);

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Release = newRelease });

        // Assert
        episode.Release.Should().Be(newRelease);
    }

    [Fact(DisplayName = "Apply sets PublishHomepage when episode is within the past week and has a change")]
    public void Apply_sets_publish_homepage_for_recent_episode_with_change()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Release = DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Title = "Updated title" });

        // Assert
        state.PublishHomepage.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply does not set PublishHomepage for an old episode with no in-week release change")]
    public void Apply_does_not_set_publish_homepage_for_old_episode()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Release = DateTime.UtcNow.AddDays(-30));
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Title = "Updated title" });

        // Assert
        state.PublishHomepage.Should().BeFalse();
    }

    [Fact(DisplayName = "Apply does not set PublishHomepage when there is no change at all")]
    public void Apply_does_not_set_publish_homepage_when_no_change()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Release = DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest());

        // Assert
        state.PublishHomepage.Should().BeFalse();
    }

    // ----- Posted / Tweeted / BlueskyPosted un-post side effects -----

    [Fact(DisplayName = "Apply sets UnPost when Posted transitions from true to false")]
    public void Apply_sets_unpost_flag()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Posted = true);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Posted = false });

        // Assert
        episode.Posted.Should().BeFalse();
        state.UnPost.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply does not set UnPost when Posted was already false")]
    public void Apply_does_not_set_unpost_when_already_false()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Posted = false);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Posted = false });

        // Assert
        state.UnPost.Should().BeFalse();
    }

    [Fact(DisplayName = "Apply sets UnTweet when Tweeted transitions from true to false")]
    public void Apply_sets_untweet_flag()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Tweeted = true);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Tweeted = false });

        // Assert
        episode.Tweeted.Should().BeFalse();
        state.UnTweet.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky is requested for an AT-URI episode, then set UnBlueskyPost but leave BlueskyPost on the episode, because delete must run before Cosmostate is cleared.")]
    public void Apply_sets_unbluesky_post_flag_and_nulls_field()
    {
        // Arrange
        const string atUri = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        var episode = CreateEpisode(e => e.BlueskyPost = atUri);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { UnBluesky = true });

        // Assert
        state.UnBlueskyPost.Should().BeTrue();
        episode.BlueskyPosted.Should().BeTrue();
        episode.BlueskyPost.Should().Be(atUri);
    }

    [Fact(DisplayName = "Apply does not set UnBlueskyPost when UnBluesky is requested but episode is not Bluesky-posted")]
    public void Apply_does_not_unbluesky_when_not_posted()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { UnBluesky = true });

        // Assert
        state.UnBlueskyPost.Should().BeFalse();
        episode.BlueskyPosted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky is requested for a legacy bluesky-flag episode, then set UnBlueskyPost but leave OldBlueskyPosted, because EpisodeUpdateService clears only after a successful delete.")]
    public void Apply_clears_legacy_old_bluesky_posted_on_unpost()
    {
        // Arrange
        var episode = CreateEpisode(e => e.OldBlueskyPosted = true);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { UnBluesky = true });

        // Assert
        state.UnBlueskyPost.Should().BeTrue();
        episode.BlueskyPosted.Should().BeTrue();
        episode.OldBlueskyPosted.Should().BeTrue();
    }

    // ----- Subjects -----

    [Fact(DisplayName = "Apply updates Subjects and sets UpdatedSubjects when the set changes")]
    public void Apply_updates_subjects()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Subjects = ["Old Subject"]);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Subjects = ["New Subject"] });

        // Assert
        episode.Subjects.Should().BeEquivalentTo(["New Subject"]);
        episode.RemovedSubjects.Should().Contain("Old Subject");
        state.UpdatedSubjects.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply does not set UpdatedSubjects when the subject set is unchanged")]
    public void Apply_does_not_flag_unchanged_subjects()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Subjects = ["Same"]);
        var sut = CreateSut();

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest { Subjects = ["Same"] });

        // Assert
        state.UpdatedSubjects.Should().BeFalse();
    }

    // ----- Guests -----

    [Fact(DisplayName = "Apply trims and filters blank Guests entries")]
    public void Apply_trims_and_filters_guests()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Guests = [" Alice ", "", "  ", "Bob"] });

        // Assert
        episode.Guests.Should().BeEquivalentTo(["Alice", "Bob"]);
    }

    [Fact(DisplayName = "Apply sets Guests to null when given an empty array")]
    public void Apply_clears_guests_on_empty_array()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Guests = ["Existing"]);
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Guests = [] });

        // Assert
        episode.Guests.Should().BeNull();
    }

    // ----- Spotify URL -----

    [Fact(DisplayName = "Apply sets SpotifyId/Urls.Spotify and UpdateSpotifyImage for a valid Spotify episode URL")]
    public void Apply_sets_spotify_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var spotifyUrl = new Uri("https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk?si=abc123");

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { Spotify = spotifyUrl }
        });

        // Assert
        episode.SpotifyId.Should().Be("4rOoJ6Egrf8K2IrywzwOMk");
        episode.Ids!.Spotify.Should().Be("4rOoJ6Egrf8K2IrywzwOMk");
        episode.Urls.Spotify!.ToString().Should().NotContain("si=");
        state.UpdateSpotifyImage.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply clears SpotifyId/Urls.Spotify/Images.Spotify when Urls.Spotify is an empty URI")]
    public void Apply_clears_spotify_url()
    {
        // Arrange
        var episode = CreateEpisode(e =>
        {
            e.SpotifyId = "existing";
            e.Urls.Spotify = new Uri("https://open.spotify.com/episode/existing");
            e.Images = new EpisodeImages { Spotify = new Uri("https://img/spotify.png") };
        });
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { Spotify = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.SpotifyId.Should().BeEmpty();
        episode.Urls.Spotify.Should().BeNull();
        episode.Images.Should().BeNull("all image fields ended up null so Images collapses to null");
    }

    // ----- Apple URL -----

    [Fact(DisplayName = "Apply sets AppleId/Urls.Apple and UpdateAppleImage for a valid Apple URL")]
    public void Apply_sets_apple_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var appleUrl = new Uri("https://podcasts.apple.com/us/podcast/some-show/id1234567890?i=9876543210");

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { Apple = appleUrl }
        });

        // Assert
        episode.AppleId.Should().Be(9876543210L);
        state.UpdateAppleImage.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply clears AppleId/Urls.Apple when Urls.Apple is an empty URI")]
    public void Apply_clears_apple_url()
    {
        // Arrange
        var episode = CreateEpisode(e =>
        {
            e.AppleId = 123;
            e.Urls.Apple = new Uri("https://podcasts.apple.com/us/podcast/some-show/id1?i=2");
        });
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { Apple = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.AppleId.Should().BeNull();
        episode.Urls.Apple.Should().BeNull();
    }

    // ----- YouTube URL -----

    [Fact(DisplayName = "Apply sets YouTubeId/Urls.YouTube and UpdateYouTubeImage for a valid YouTube URL")]
    public void Apply_sets_youtube_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var youTubeUrl = new Uri("https://youtu.be/AB-CDEFG123");

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { YouTube = youTubeUrl }
        });

        // Assert
        episode.YouTubeId.Should().Be("AB-CDEFG123");
        episode.Urls.YouTube!.ToString().Should().Contain("AB-CDEFG123");
        state.UpdateYouTubeImage.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply clears YouTubeId/Urls.YouTube when Urls.YouTube is an empty URI")]
    public void Apply_clears_youtube_url()
    {
        // Arrange
        var episode = CreateEpisode(e =>
        {
            e.YouTubeId = "existing";
            e.Urls.YouTube = new Uri("https://youtu.be/existing");
        });
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { YouTube = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.YouTubeId.Should().BeEmpty();
        episode.Urls.YouTube.Should().BeNull();
    }

    // ----- BBC / InternetArchive URLs -----

    [Fact(DisplayName = "Apply sets Urls.BBC and UpdateBBCImage for a valid BBC iPlayer URL")]
    public void Apply_sets_bbc_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var bbcUrl = new Uri("https://www.bbc.co.uk/iplayer/episode/p0abcd12");

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { BBC = bbcUrl }
        });

        // Assert
        episode.Urls.BBC.Should().Be(bbcUrl);
        state.UpdateBBCImage.Should().BeTrue();
    }

    [Fact(DisplayName = "Apply clears Urls.BBC when set to an empty URI")]
    public void Apply_clears_bbc_url()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Urls.BBC = new Uri("https://www.bbc.co.uk/iplayer/episode/p0abcd12"));
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { BBC = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.Urls.BBC.Should().BeNull();
    }

    [Fact(DisplayName = "Apply sets Urls.InternetArchive for a valid archive.org details URL")]
    public void Apply_sets_internet_archive_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var archiveUrl = new Uri("https://archive.org/details/some-episode-id");

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { InternetArchive = archiveUrl }
        });

        // Assert
        episode.Urls.InternetArchive.Should().Be(archiveUrl);
    }

    [Fact(DisplayName = "Apply clears Urls.InternetArchive when set to an empty URI")]
    public void Apply_clears_internet_archive_url()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Urls.InternetArchive = new Uri("https://archive.org/details/some-episode-id"));
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Urls = new ServiceUrls { InternetArchive = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.Urls.InternetArchive.Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply stores a Vimeo URL adjacent to its image under services.vimeo. Extra service art is not written to leftover Images.Other.")]
    public void Apply_sets_vimeo_service_url_and_image_adjacent()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var videoId = _fixture.CreateAppleId();
        var vimeoUrl = new Uri($"https://vimeo.com/{videoId}");
        var vimeoImage = _fixture.Create<Uri>();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Services = new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Vimeo] = new() { Url = vimeoUrl, Image = vimeoImage }
            }
        });

        // Assert
        episode.Services.Should().ContainKey(ServiceKeys.Vimeo);
        episode.Services![ServiceKeys.Vimeo].Url.Should().Be(vimeoUrl);
        episode.Services[ServiceKeys.Vimeo].Image.Should().Be(vimeoImage);
        episode.Images?.Other.Should().BeNull();
        EpisodeServicePresence.ToEpisodeImages(episode).Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply ignores leftover Images.Other on the change request, so extra-service art is not written to images.other.")]
    public void Apply_does_not_write_leftover_images_other()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var leftover = _fixture.Create<Uri>();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Images = new ServiceImageUrls { Other = leftover }
        });

        // Assert
        episode.Images?.Other.Should().BeNull();
        EpisodeServicePresence.ToEpisodeImages(episode).Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply extracts SpotifyId into nested ids when a Spotify URL is supplied under services, so presence of that service is the id rather than a named URL slot.")]
    public void Apply_extracts_spotify_id_from_services_url()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var spotifyUrl = new Uri("https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk");

        // Act
        var state = sut.Apply(episode, new EpisodeChangeRequest
        {
            Services = new Dictionary<string, EpisodeServiceLink>
            {
                ["spotify"] = new() { Url = spotifyUrl }
            }
        });

        // Assert
        episode.SpotifyId.Should().Be("4rOoJ6Egrf8K2IrywzwOMk");
        episode.Ids!.Spotify.Should().Be("4rOoJ6Egrf8K2IrywzwOMk");
        episode.Services.Should().ContainKey("spotify");
        state.UpdateSpotifyImage.Should().BeTrue();
    }

    // ----- Images -----

    [Fact(DisplayName = "Apply sets an individual image field, creating Images lazily")]
    public void Apply_sets_individual_image()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();
        var imageUri = new Uri("https://img.example.com/apple.png");

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Images = new ServiceImageUrls { Apple = imageUri }
        });

        // Assert
        episode.Images.Should().NotBeNull();
        episode.Images!.Apple.Should().Be(imageUri);
    }

    [Fact(DisplayName = "Apply collapses Images back to null once every image field is cleared")]
    public void Apply_collapses_images_to_null_when_all_cleared()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Images = new EpisodeImages { Apple = new Uri("https://img/apple.png") });
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest
        {
            Images = new ServiceImageUrls { Apple = new Uri("", UriKind.Relative) }
        });

        // Assert
        episode.Images.Should().BeNull();
    }
}
