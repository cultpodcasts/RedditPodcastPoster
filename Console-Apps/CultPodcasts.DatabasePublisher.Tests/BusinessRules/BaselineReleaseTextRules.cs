using System.Globalization;
using System.Text.Json;
using CultPodcasts.DatabasePublisher.PublicModels;
using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Providers;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class BaselineReleaseTextRules : IDisposable
{
    private const string Midnight = "2026-08-17T00:00:00Z";
    private const string Afternoon = "2026-08-17T10:30:15Z";

    private readonly PublisherScenario _scenario = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "public-db-release-" + Guid.NewGuid());

    public BaselineReleaseTextRules() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _scenario.Dispose();
        Directory.Delete(_folder, true);
    }

    [Theory(DisplayName =
        "Release text: when an episode is already in the previous file with a release that stands for the same " +
        "instant, then the previous text is published exactly as it was (no Z, a Z, an offset, extra fractional " +
        "digits or a bare date), because a published file must not change just because of how a date-time is written.")]
    [InlineData("2026-08-17T00:00:00", Midnight)]
    [InlineData("2026-08-17", Midnight)]
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

    [Theory(DisplayName =
        "Release text: when an episode is in the previous file but its release is a different instant, then the " +
        "newly formatted release is published, because a real change of release must reach the file.")]
    [InlineData("2026-08-16T00:00:00", Midnight)]
    [InlineData("2026-08-17T10:30:16Z", Afternoon)]
    [InlineData("2026-08-17T10:30:15+01:00", Afternoon)]
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
        "Release text: when the previous release text is not a date-time (words, an impossible date, empty, a " +
        "year, another date format, a number or null), then the newly formatted release is published, because " +
        "nothing can be said to be the same instant.")]
    [InlineData("not a date")]
    [InlineData("2026-13-45")]
    [InlineData("")]
    [InlineData("2026")]
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
        "Release text: when the previous file is absent, is not JSON, is not an object or has no episodes, then the " +
        "episode's release is written exactly as it always has been, because without a readable baseline there is " +
        "nothing to keep.")]
    [InlineData(null)]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "episodes": "none" }""")]
    public async Task absent_or_unreadable_previous_file_changes_nothing(string? previousFile)
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var id = Guid.NewGuid();
        var podcast = new PublicPodcast(Guid.NewGuid())
        {
            FileKey = "podcast",
            Episodes = [new PublicEpisode { Id = id, Release = Utc(Midnight) }]
        };
        if (previousFile is not null)
        {
            File.WriteAllText(Path.Combine(_folder, "podcast.json"), previousFile);
        }

        // Act
        await folder.StageAsync(podcast);

        // Assert
        PublishedRelease(Path.Combine(_folder, "podcast.json.tmp"), "episodes", id, isPath: true).Should().Be(Midnight);
    }

    [Fact(DisplayName =
        "Release text: when a baseline file key is given for staging, then that file is the baseline instead of the " +
        "target's own, because a moved file's old name is where the previous text is.")]
    public async Task staging_reads_the_baseline_from_the_given_key()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var id = Guid.NewGuid();
        File.WriteAllText(
            Path.Combine(_folder, "old-key.json"),
            JsonSerializer.Serialize(new { id = Guid.NewGuid(), episodes = new[] { new { id, release = "2026-08-17T00:00:00" } } }));
        var podcast = new PublicPodcast(Guid.NewGuid())
        {
            FileKey = "new-key",
            Episodes = [new PublicEpisode { Id = id, Release = Utc(Midnight) }]
        };

        // Act
        await folder.StageAsync(podcast, "old-key");

        // Assert
        PublishedRelease(Path.Combine(_folder, "new-key.json.tmp"), "episodes", id, isPath: true).Should().Be("2026-08-17T00:00:00");
    }

    private string PublishedRelease(string fileKeyOrPath, string children, Guid id, bool isPath = false)
    {
        var text = isPath ? File.ReadAllText(fileKeyOrPath) : _scenario.Files[fileKeyOrPath];
        using var document = JsonDocument.Parse(text);
        return document.RootElement.GetProperty(children).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("release").GetString()!;
    }

    private static DateTime Utc(string instant) =>
        DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
