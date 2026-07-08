using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Cross-platform matching rules for YouTube release authority podcasts and ambiguous merge detection.
/// </summary>
public class CrossPlatformMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "For YouTube release authority podcasts, an Apple catalogue episode may match a YouTube-only stored episode " +
        "when title and duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.")]
    public void YouTube_release_authority_Apple_catalogue_matches_YouTube_only_stored_episode()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, appleId) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityApplePair(podcast);
        var expected = EpisodeExpectation.From(stored)
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts, a Spotify catalogue episode may match a YouTube-only stored episode " +
        "when title and duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.")]
    public void YouTube_release_authority_Spotify_catalogue_matches_YouTube_only_stored_episode()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, spotifyId) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, episodes must not merge on " +
        "release-and-duration alone when titles share no fuzzy or substring relationship.")]
    public void Negative_delay_does_not_merge_on_release_and_duration_when_titles_differ()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);
        var expected = EpisodeExpectation.From(stored);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        result.AddedEpisodes.Single().Id.Should().NotBe(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When two stored episodes could both match an incoming episode, indexing must record merge failure " +
        "— not pick arbitrarily.")]
    public void Ambiguous_match_records_failed_episodes_instead_of_picking_one()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var sharedRelease = DomainTestFixture.UtcDaysAgo(32);
        var sharedLength = _fixture.CreateDuration();
        var sharedTitle = _fixture.CreateTitle();
        var (youTubeOnly, appleOnly) = _fixture.CreateAmbiguousMatchStoredEpisodes(
            podcast,
            sharedRelease,
            sharedLength,
            sharedTitle);
        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(
            sharedRelease,
            sharedLength,
            sharedTitle);

        // Act
        var result = _merger.MergeEpisodes(podcast, [youTubeOnly, appleOnly], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().ContainSingle();
        var failedCandidates = result.FailedEpisodes.Single().ToList();
        failedCandidates.Should().HaveCount(2);
        failedCandidates.Should().Contain(x => x.Id == youTubeOnly.Id);
        failedCandidates.Should().Contain(x => x.Id == appleOnly.Id);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, an incoming YouTube episode " +
        "may match a stored audio episode when release aligns after delay adjustment.")]
    public void Positive_YouTube_delay_matches_incoming_YouTube_to_stored_audio_episode()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            channelId: _fixture.CreateYouTubeChannelId(),
            youTubePublicationOffsetTicks: publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var length = _fixture.CreateDuration();
        var stored = _fixture.CreatePositiveDelayAudioStoredEpisode(
            podcast,
            audioRelease: audioRelease,
            length: length);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(_fixture.CreateTitle())
            .WithRelease(youTubeInput.Release)
            .WithDuration(length));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For Spotify-first podcasts with positive YouTube publishing delay, an incoming YouTube episode " +
        "may match a stored audio episode that has a descriptive title when publish aligns after delay " +
        "adjustment and duration is within cross-platform tolerance — even when titles differ substantially.")]
    public void Spotify_first_positive_delay_youtube_incoming_merges_descriptive_stored_audio()
    {
        // Arrange — IndoctriNation-shaped: Reddit/descriptive stored title vs YouTube suffix title
        const string storedTitle =
            "Holy Disobedience: Inside the Seventh-day Adventist Church with Melissa Duge Spiers";
        const string youTubeTitle =
            "Holy Disobedience: Inside the Seventh-day Adventist Church with Melissa Duge Spiers | IndoctriNation";
        var publishingDelay = TimeSpan.FromDays(1);
        var audioRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = TimeSpan.FromMinutes(83);
        var incomingLength = storedLength + TimeSpan.FromMinutes(3);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Name = "IndoctriNation";
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePlaylistId = "PLNAi0T2hzH9dM029dkTfj635PzuXl67jS";
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var spotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(storedTitle)
            .WithRelease(audioRelease)
            .WithLength(storedLength)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(youTubeTitle)
            .WithRelease(audioRelease.Add(publishingDelay).AddHours(10))
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(youTubeInput.Title)
            .WithRelease(youTubeInput.Release)
            .WithDuration(incomingLength));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When a Spotify catalogue episode uses the podcast show name as its title, indexing may still merge " +
        "onto a stored descriptive episode on release and duration alignment when the stored row has no Spotify id.")]
    public void Generic_spotify_show_name_title_merges_onto_descriptive_stored_episode_without_spotify_id()
    {
        // Arrange
        const string storedTitle =
            "Holy Disobedience: Inside the Seventh-day Adventist Church with Melissa Duge Spiers";
        const string catalogueTitle = "IndoctriNation";
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var length = TimeSpan.FromMinutes(83);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Name = catalogueTitle;
        var stored = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = storedTitle;
            e.Release = release;
            e.Length = length;
            e.SpotifyId = string.Empty;
            e.Urls = new ServiceUrls();
        });
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(catalogueTitle)
            .WithRelease(release)
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(catalogueTitle)
            .WithRelease(release)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithDuration(length));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When a stored Apple-only episode and an incoming YouTube playlist episode share descriptive title " +
        "relationship, same calendar day, configured offset, and duration within five minutes, indexing must merge.")]
    public void Apple_stored_youtube_incoming_merges_with_show_name_prefix_and_loose_offset_alignment()
    {
        // Arrange — audio-first drop with two-hour offset; YouTube publishes ~1h5m after audio (before expected +2h)
        const string coreTitle =
            "Holy Disobedience: Inside the Seventh-day Adventist Church with Melissa Duge Spiers";
        const string youTubeTitle = "IndoctriNation - " + coreTitle;
        var publishingDelay = TimeSpan.FromHours(2);
        var audioRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(14));
        var youTubeRelease = audioRelease.Add(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5));
        var storedLength = TimeSpan.FromMinutes(79) + TimeSpan.FromSeconds(25);
        var incomingLength = storedLength + TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(55);
        var appleId = _fixture.CreateAppleId();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(coreTitle)
            .WithRelease(audioRelease)
            .WithLength(storedLength)
            .WithApple(appleId, new Uri($"https://podcasts.apple.com/podcast/id1373939526?i={appleId}"))
            .Create();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(youTubeTitle)
            .WithRelease(youTubeRelease)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(youTubeTitle)
            .WithRelease(youTubeRelease)
            .WithDuration(incomingLength));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For cross-platform YouTube-to-audio-stored pairs, duration tolerance is five minutes (strict less-than); " +
        "differences beyond that must not merge on release alignment alone.")]
    public void Cross_platform_duration_beyond_five_minutes_does_not_merge_on_release_alignment()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var audioRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = TimeSpan.FromMinutes(83);
        var incomingLength = storedLength + TimeSpan.FromMinutes(5);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var spotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(_fixture.CreateTitle())
            .WithRelease(audioRelease)
            .WithLength(storedLength)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(incomingLength));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithRelease(youTubeInput.Release)
            .WithDuration(incomingLength));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
    }
}
