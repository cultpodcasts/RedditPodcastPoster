using FluentAssertions;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

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
        "For YouTube release authority podcasts with negative publishing delay, a YouTube-only stored episode " +
        "and an Apple catalogue episode with delay-aligned releases and duration within five minutes must merge " +
        "even when marketing titles are wholly disjoint — release+duration scores meet the cross-platform threshold.")]
    public void Negative_delay_aligned_divergent_titles_merge_on_release_and_duration_score()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, appleId) = _fixture.CreateNegativeDelayAlignedDivergentTitlePair(podcast);
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
        "After Apple has already merged onto the YouTube row, an incoming Spotify catalogue episode with " +
        "delay-aligned release and duration within five minutes must still merge onto that YouTube+Apple " +
        "target despite wholly divergent marketing titles.")]
    public void Spotify_merges_onto_youtube_plus_apple_when_delay_aligned_despite_divergent_titles()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromHours(-8.5).Ticks;
        var youTubeRelease = new DateTime(2026, 7, 13, 4, 0, 5, DateTimeKind.Utc);
        var audioRelease = new DateTime(2026, 7, 13, 8, 30, 0, DateTimeKind.Utc);
        var youTubeLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(37);
        var spotifyLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(39);
        var appleId = _fixture.CreateAppleId();
        var youTubeId = _fixture.CreateYouTubeId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle("The Neighborhood Scheme: Shocking Truth About Wellness Influencer Networks")
            .WithRelease(youTubeRelease)
            .WithLength(youTubeLength)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(
                "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost")
            .WithRelease(audioRelease)
            .WithDuration(spotifyLength));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(spotifyInput.Title)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(audioRelease)
            .WithDuration(spotifyLength));
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);

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
        "Spotify for this week's episode must not merge onto last week's YouTube episode even when " +
        "catalogue-day tolerance (±5) and five-minute duration include that row — composite score stays " +
        "below threshold without delay alignment or title confidence.")]
    public void Spotify_does_not_merge_onto_wrong_week_youtube_under_weak_catalogue_day_alignment()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromHours(-8.5).Ticks;
        var lastWeekYouTubeRelease = new DateTime(2026, 7, 10, 19, 0, 46, DateTimeKind.Utc);
        var thisWeekSpotifyRelease = new DateTime(2026, 7, 13, 8, 30, 0, DateTimeKind.Utc);
        var lastWeekLength = TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(40);
        var thisWeekSpotifyLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(39);
        var youTubeId = _fixture.CreateYouTubeId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle("Civic turnout strategies for mid-cycle ballot measures")
            .WithRelease(lastWeekYouTubeRelease)
            .WithLength(lastWeekLength)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .Create();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(
                "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost")
            .WithRelease(thisWeekSpotifyRelease)
            .WithDuration(thisWeekSpotifyLength));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(spotifyInput.Title)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(thisWeekSpotifyRelease)
            .WithDuration(thisWeekSpotifyLength));
        var expected = EpisodeExpectation.From(stored);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    
    [Fact(DisplayName =
        "A Spotify catalogue episode with delay-aligned release and duration within five minutes must " +
        "merge onto a YouTube-only stored episode despite wholly divergent marketing titles.")]
    public void Spotify_merges_onto_youtube_only_when_delay_aligned_despite_divergent_titles()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, spotifyId) = _fixture.CreateNegativeDelayAlignedDivergentTitleSpotifyPair(podcast);
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
        "When ExactReleaseMatchStrategy accepts same-calendar-day audio-stored and YouTube-incoming releases " +
        "(not delay-aligned), duration within five minutes meets the cross-platform score threshold even when " +
        "titles are disjoint.")]
    public void Same_calendar_day_release_and_duration_merges_despite_divergent_titles()
    {
        // Arrange — positive delay expects YouTube next day; same calendar day is a separate ExactRelease signal.
        var publishingDelay = TimeSpan.FromDays(3);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(10));
        // Same calendar day as audio, but far from expected audio+delay (outside 1-day align window).
        var youTubeRelease = audioRelease.Date.Add(TimeSpan.FromHours(18));
        var length = TimeSpan.FromMinutes(70);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var spotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle("Alpha market briefing on early catalogue drift signals")
            .WithRelease(audioRelease)
            .WithLength(length)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle("Omega wellness interview about unrelated guest journeys")
            .WithRelease(youTubeRelease)
            .WithDuration(length - TimeSpan.FromSeconds(2)));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(youTubeInput.Title)
            .WithRelease(youTubeInput.Release)
            .WithDuration(youTubeInput.Duration));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
                audioRelease, youTubeRelease, publishingDelay)
            .Should().BeFalse();
        EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease)
            .Should().BeTrue();

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Weak catalogue-day release alignment plus similar duration can still merge when a substring title " +
        "relationship supplies enough score to reach the cross-platform threshold.")]
    public void Weak_catalogue_day_alignment_merges_when_substring_title_pushes_score_over_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var audioRelease = (youTubeRelease - delay).Date.AddDays(3);
        var length = TimeSpan.FromMinutes(55);
        const string core = "Holy Disobedience: Inside Adventist Networks";
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, youTubeRelease, length, core);
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle("Show Prefix - " + core)
            .WithRelease(audioRelease)
            .WithDuration(length));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(spotifyInput.Title)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(audioRelease)
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        result.AddedEpisodes.Should().BeEmpty();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For Spotify-first podcasts with positive YouTube publishing delay, delay-aligned release and duration " +
        "within five minutes merge YouTube onto stored audio even when marketing titles are wholly disjoint.")]
    public void Positive_delay_aligned_divergent_titles_merge_on_release_and_duration_score()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(10));
        var length = TimeSpan.FromMinutes(70);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var spotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle("Alpha market briefing on early catalogue drift signals")
            .WithRelease(audioRelease)
            .WithLength(length)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle("Omega wellness interview about unrelated guest journeys")
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(length - TimeSpan.FromSeconds(2)));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(youTubeInput.Title)
            .WithRelease(youTubeInput.Release)
            .WithDuration(youTubeInput.Duration));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

[Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, weak catalogue-day release " +
        "alignment (±5 days) plus similar duration must not merge when titles share no fuzzy or substring " +
        "relationship — composite score stays below the cross-platform threshold.")]
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
        "When audio arrives early within a large negative YouTube publication offset " +
        "(YouTube first, Spotify ~13d later vs −31.5d expectation) and titles still diverge, matching " +
        "descriptions plus duration must merge onto the YouTube row.")]
    public void Early_within_negative_delay_divergent_titles_merge_when_descriptions_match()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, spotifyId) =
            _fixture.CreateYouTubeAuthorityNegativeOffsetEarlyAudioPair(podcast, matchingTitles: false);
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
        "When YouTube has already been renamed to match Spotify/Apple, early-within-negative-delay " +
        "audio must merge even without relying on description confidence.")]
    public void Early_within_negative_delay_renamed_titles_merge()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, spotifyId) =
            _fixture.CreateYouTubeAuthorityNegativeOffsetEarlyAudioPair(podcast, matchingTitles: true);
        stored.Description = "YouTube teaser copy that no longer matches the Spotify show notes.";
        discovered.Description = "Spotify-only RSS description with wholly different wording.";
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
        "Early-within-negative-delay release plus similar duration must not merge when titles and " +
        "descriptions are both divergent — keep #869 protection across the widened delay window.")]
    public void Early_within_negative_delay_does_not_merge_without_title_or_description_confidence()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromDays(-31).Add(TimeSpan.FromHours(-12)).Ticks;
        var youTubeRelease = new DateTime(2026, 7, 1, 15, 21, 27, DateTimeKind.Utc);
        var audioRelease = new DateTime(2026, 7, 14, 13, 0, 0, DateTimeKind.Utc);
        var length = TimeSpan.FromMinutes(80);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            length,
            "Civic turnout strategies for mid-cycle ballot measures");
        stored.Description = "Ballot-measure strategy notes with no overlap to the audio episode.";
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(
                "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost")
            .WithDescription("Wellness-scheme interview notes with no shared ballot wording.")
            .WithRelease(audioRelease)
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
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
