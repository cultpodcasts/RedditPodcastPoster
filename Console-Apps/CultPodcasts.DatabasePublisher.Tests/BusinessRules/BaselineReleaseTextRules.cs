using System.Globalization;
using System.Text.Json;
using CultPodcasts.DatabasePublisher.Consistency;
using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class BaselineReleaseTextRules : IDisposable
{
    private const string Midnight = "2026-08-17T00:00:00Z";
    private const string Afternoon = "2026-08-17T10:30:15Z";

    private readonly PublisherScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    [Theory(DisplayName =
        "Release text: when an episode is already in the previous file with a full date-time release that stands " +
        "for the same instant, then the previous text is published exactly as it was (no Z, a Z, an offset or extra " +
        "fractional digits), because a published file must not change just because of how a date-time is written.")]
    [InlineData("2026-08-17T00:00:00", Midnight)]
    [InlineData("2026-08-17T00:00:00Z", Midnight)]
    [InlineData("2026-08-17T10:30:15", Afternoon)]
    [InlineData("2026-08-17T10:30:15Z", Afternoon)]
    [InlineData("2026-08-17T10:30:15.0000000Z", Afternoon)]
    [InlineData("2026-08-17T11:30:15+01:00", Afternoon)]
    public async Task same_instant_keeps_the_previous_text(string previous, string current)
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(current));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (episode.Id, previous));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be(previous);
    }

    [Fact(DisplayName =
        "Release text: when the kept previous text has an offset, then the file holds it the way the serializer " +
        "escapes it and publishing again changes nothing, because the check must be on the bytes written and not " +
        "only on the text read back.")]
    public async Task kept_offset_text_is_written_as_the_serializer_writes_it_and_is_stable()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Afternoon));
        _scenario.GivenExistingFileWithChildren(
            podcast.FileKey, podcast.Id, "episodes", (episode.Id, "2026-08-17T11:30:15+01:00"));

        // Act
        await _scenario.Publish();
        var first = _scenario.Files[podcast.FileKey];
        await _scenario.Publish();

        // Assert
        first.Should().Contain("2026-08-17T11:30:15\\u002B01:00");
        _scenario.Files[podcast.FileKey].Should().Be(first);
    }

    [Theory(DisplayName =
        "Release text: when an episode is in the previous file but its release is a different instant, then the " +
        "newly formatted release is published, because a real change of release must reach the file.")]
    [InlineData("2026-08-16T00:00:00", Midnight)]
    [InlineData("2026-08-17T10:30:16Z", Afternoon)]
    [InlineData("2026-08-17T10:30:15+01:00", Afternoon)]
    [InlineData("2026-08-17T10:30:15.1234567Z", Afternoon)]
    public async Task different_instant_publishes_the_new_text(string previous, string current)
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(current));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (episode.Id, previous));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be(current);
    }

    [Fact(DisplayName =
        "Release text: when an episode is new to the file, then its release is written as it always has been for a " +
        "UTC value, with a trailing Z, and an episode that was already there keeps its own previous text, because " +
        "only existing episodes are held to the previous format.")]
    public async Task new_episode_gets_the_new_text_beside_a_kept_one()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var existing = _scenario.GivenEpisode(podcast, releasedDaysAgo: 2, e => e.ReleaseUtc = Utc(Midnight));
        var added = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Afternoon));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (existing.Id, "2026-08-17T00:00:00"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", existing.Id).Should().Be("2026-08-17T00:00:00");
        PublishedRelease(podcast.FileKey, "episodes", added.Id).Should().Be(Afternoon);
    }

    [Theory(DisplayName =
        "Release text: when the previous release text is not a full date-time (words, an impossible date, empty, a " +
        "year, a bare date, a time without seconds or with a space, an offset without minutes, more than seven " +
        "fractional digits, another date format, a number or null), then the newly formatted release is " +
        "published, because nothing can be said to be the same instant, and a bare date is never held against a " +
        "date-time.")]
    [InlineData("not a date")]
    [InlineData("2026-13-45")]
    [InlineData("2026-08-17T25:00:00")]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-08-17")]
    [InlineData("2026-08-17T00:00")]
    [InlineData("2026-08-17T 00:00:00")]
    [InlineData("2026-08-17T00:00:00+00")]
    [InlineData("2026-08-17T00:00:00.00000000Z")]
    [InlineData("2026-08-17T00:00:00Z\n")]
    [InlineData("17/08/2026")]
    [InlineData(2026)]
    [InlineData(null)]
    public async Task unusable_previous_text_publishes_the_new_text(object? previous)
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Midnight));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (episode.Id, previous));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be(Midnight);
    }

    [Theory(DisplayName =
        "Release text: when the current culture is not the invariant one (a Buddhist, Um al-Qura or Turkish " +
        "calendar and digits), then the previous text is still compared as the instant it is, because a " +
        "regression to culture-sensitive parsing must not change what is published.")]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("tr-TR")]
    public async Task comparison_does_not_depend_on_the_current_culture(string culture)
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Afternoon));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (episode.Id, "2026-08-17T10:30:15"));
        var original = CultureInfo.CurrentCulture;

        // Act
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            await _scenario.Publish();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be("2026-08-17T10:30:15");
    }

    [Fact(DisplayName =
        "Release text: when the previous file has two children with the same id, then the first one's text is the " +
        "one used, because the file is read once and a repeated id must not change which text wins from run to run.")]
    public async Task first_of_two_children_with_the_same_id_wins()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Midnight));
        _scenario.GivenExistingFileWithChildren(
            podcast.FileKey, podcast.Id, "episodes",
            (episode.Id, "2026-08-17T00:00:00"),
            (episode.Id, "2026-08-17T00:00:00Z"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be("2026-08-17T00:00:00");
    }

    [Fact(DisplayName =
        "Release text: when a file is published again over the files of the previous publish, then it is the same " +
        "bytes as before apart from real changes, even where the previous publish wrote some releases without a Z, " +
        "because that is what keeps the repository free of churn.")]
    public async Task republishing_over_a_file_with_unmarked_releases_leaves_it_byte_identical()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 3, e => e.ReleaseUtc = Utc(Midnight));
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 2, e => e.ReleaseUtc = Utc(Afternoon));
        await _scenario.PublishFirstTime();
        var unmarked = _scenario.Files[podcast.FileKey].Replace("T00:00:00Z\"", "T00:00:00\"");
        unmarked.Should().NotBe(_scenario.Files[podcast.FileKey]);
        File.WriteAllText(Path.Combine(_scenario.OutputDirectory, podcast.FileKey + ".json"), unmarked);

        // Act
        await _scenario.Publish();

        // Assert
        _scenario.Files[podcast.FileKey].Should().Be(unmarked);
    }

    [Fact(DisplayName =
        "Release text: when the previous file's key is now published for a different item (a different root id), " +
        "then none of its texts are used, because another item's text must never be applied if a file key is " +
        "reused.")]
    public async Task a_previous_file_of_a_different_item_gives_no_text()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Midnight));
        var otherItem = Guid.NewGuid();
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, otherItem, "episodes", (episode.Id, "2026-08-17T00:00:00"));

        // Act
        await _scenario.Publish(new PublishOptions(AcceptedRemovals: [otherItem.ToString()]));

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be(Midnight);
    }

    [Fact(DisplayName =
        "Release text: when a podcast became a TV show and the show's episode was in the podcast's file with the " +
        "same release instant, then the show file keeps that previous text, because the file is moved and " +
        "overwritten and its dates should not change only because of a Z.")]
    public async Task migrated_tv_show_keeps_the_previous_release_text()
    {
        // Arrange
        var transferred = _scenario.GivenPodcast();
        _scenario.Podcasts.Remove(transferred);
        var show = _scenario.GivenTvShowFrom(transferred);
        var episode = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1,
            e => e.Release = CatalogueRelease.FromDateTimeUtc(Utc(Midnight)));
        _scenario.GivenExistingFileWithChildren(transferred.FileKey, transferred.Id, "episodes", (episode.Id, "2026-08-17T00:00:00"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(show.FileKey, "episodes", episode.Id).Should().Be("2026-08-17T00:00:00");
    }

    [Fact(DisplayName =
        "Release text: when a podcast became a news organisation and the report was in the podcast's file with the " +
        "same release instant, then the organisation file keeps that previous text, because a migration to a news " +
        "organisation reads the old podcast file's episodes as its reports' baseline.")]
    public async Task migrated_news_organisation_keeps_the_previous_release_text()
    {
        // Arrange
        var transferred = _scenario.GivenPodcast();
        _scenario.Podcasts.Remove(transferred);
        var organisation = _scenario.GivenNewsOrganisationFrom(transferred);
        var report = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1,
            r => r.Release = CatalogueRelease.FromDateTimeUtc(Utc(Midnight)));
        _scenario.GivenExistingFileWithChildren(transferred.FileKey, transferred.Id, "episodes", (report.Id, "2026-08-17T00:00:00"));

        // Act
        var published = await _scenario.Publish();

        // Assert
        published.Plan.Moves.Should().ContainSingle().Which.MoveTo.Should().Be(organisation.FileKey);
        PublishedRelease(organisation.FileKey, "reports", report.Id).Should().Be("2026-08-17T00:00:00");
    }

    [Fact(DisplayName =
        "Release text: when a TV show episode was published with only a date and its release is now a date-time at " +
        "midnight, then the date-time is published, because a catalogue release that gains a time of day must not " +
        "be held at its old, coarser text.")]
    public async Task tv_show_episode_whose_precision_grew_publishes_the_new_text()
    {
        // Arrange
        var show = _scenario.GivenTvShow();
        var episode = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1,
            e => e.Release = CatalogueRelease.FromDateTimeUtc(Utc("2019-05-01T00:00:00Z")));
        _scenario.GivenExistingFileWithChildren(show.FileKey, show.Id, "episodes", (episode.Id, "2019-05-01"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(show.FileKey, "episodes", episode.Id).Should().Be("2019-05-01T00:00:00Z");
    }

    [Fact(DisplayName =
        "Release text: when a news report was published with only a date and its release is now a date-time at " +
        "midnight, then the date-time is published, because the new kinds follow the same precision rule.")]
    public async Task news_report_whose_precision_grew_publishes_the_new_text()
    {
        // Arrange
        var organisation = _scenario.GivenNewsOrganisation();
        var report = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1,
            r => r.Release = CatalogueRelease.FromDateTimeUtc(Utc("2019-05-01T00:00:00Z")));
        _scenario.GivenExistingFileWithChildren(organisation.FileKey, organisation.Id, "reports", (report.Id, "2019-05-01"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(organisation.FileKey, "reports", report.Id).Should().Be("2019-05-01T00:00:00Z");
    }

    [Fact(DisplayName =
        "Release text: when a news organisation already has a report with the same release instant in its file, " +
        "then the previous text is kept and a report that is new takes the new text, because the new kinds follow " +
        "the same no-churn rule.")]
    public async Task news_report_keeps_the_previous_release_text()
    {
        // Arrange
        var organisation = _scenario.GivenNewsOrganisation();
        var existing = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 2,
            r => r.Release = CatalogueRelease.FromDateTimeUtc(Utc(Afternoon)));
        var added = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1,
            r => r.Release = CatalogueRelease.FromDateTimeUtc(Utc(Midnight)));
        _scenario.GivenExistingFileWithChildren(organisation.FileKey, organisation.Id, "reports", (existing.Id, "2026-08-17T10:30:15"));

        // Act
        await _scenario.Publish();

        // Assert
        PublishedRelease(organisation.FileKey, "reports", existing.Id).Should().Be("2026-08-17T10:30:15");
        PublishedRelease(organisation.FileKey, "reports", added.Id).Should().Be(Midnight);
    }

    [Theory(DisplayName =
        "Release text: when the previous file is not JSON, is not an object or has no episodes, then the episode's " +
        "release is written exactly as it always has been, because without a readable baseline there is nothing to " +
        "keep (the file has no id, so it is accepted as an unexplained removal).")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "episodes": "none" }""")]
    public async Task previous_file_of_the_wrong_shape_gives_no_text(string previousFile)
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Midnight));
        File.WriteAllText(Path.Combine(_scenario.OutputDirectory, podcast.FileKey + ".json"), previousFile);

        // Act
        await _scenario.Publish(new PublishOptions(AcceptedRemovals: [podcast.FileKey]));

        // Assert
        PublishedRelease(podcast.FileKey, "episodes", episode.Id).Should().Be(Midnight);
    }

    [Fact(DisplayName =
        "Release text: when the previous file exists but cannot be read (it is locked), then the publish fails with " +
        "the folder exception and changes nothing, because quietly publishing every date afresh would put " +
        "thousands of lines of churn into the repository.")]
    public async Task a_locked_previous_file_fails_the_publish_and_changes_nothing()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.ReleaseUtc = Utc(Midnight));
        _scenario.GivenExistingFileWithChildren(podcast.FileKey, podcast.Id, "episodes", (episode.Id, "2026-08-17T00:00:00"));
        var path = Path.Combine(_scenario.OutputDirectory, podcast.FileKey + ".json");
        var before = File.ReadAllText(path);

        // Act
        Func<Task> publish;
        await using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            publish = () => _scenario.Publish();
            await publish.Should().ThrowAsync<PublicationFolderException>();
        }

        // Assert
        _scenario.StagedFileNames.Should().BeEmpty();
        _scenario.FileNames.Should().Equal(podcast.FileKey + ".json");
        File.ReadAllText(path).Should().Be(before);
    }

    private string PublishedRelease(string fileKey, string children, Guid id)
    {
        using var document = JsonDocument.Parse(_scenario.Files[fileKey]);
        return document.RootElement.GetProperty(children).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("release").GetString()!;
    }

    private static DateTime Utc(string instant) =>
        DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}