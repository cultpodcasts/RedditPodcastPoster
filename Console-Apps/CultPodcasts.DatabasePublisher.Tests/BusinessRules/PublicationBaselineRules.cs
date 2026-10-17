using CultPodcasts.DatabasePublisher.Consistency;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class PublicationBaselineRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    [Fact(DisplayName =
        "Publication baseline: when a podcast was moved to a TV show, then its old file is reported as migrated " +
        "to the new tvshow- file (same id) and the run succeeds, because the transfer deletes the podcast rows but " +
        "the item is still published and consumers need to see the file change name.")]
    public async Task migrated_podcast_is_reported_and_allowed()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.WriteBaselineFile(podcast.FileKey, podcast.Id);
        _scenario.Podcasts.Clear();
        var show = _scenario.GivenTvShowFrom(podcast);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);

        // Act
        var report = await _scenario.Publish(Baseline());

        // Assert
        var removal = report.Comparison!.Removals.Should().ContainSingle().Subject;
        removal.Reason.Should().Be(RemovalReason.Migrated);
        removal.FileKey.Should().Be(podcast.FileKey);
        removal.Id.Should().Be(podcast.Id);
        removal.Successor!.FileKey.Should().Be(show.FileKey);
        removal.Successor.Kind.Should().Be(PublicKind.TvShow);
        report.Comparison.Unexplained.Should().BeEmpty();
        _scenario.Writer.Files.Keys.Should().BeEquivalentTo(show.FileKey);
    }

    [Fact(DisplayName =
        "Publication baseline: when a podcast changed its file key but kept its kind, then the old file is " +
        "reported as renamed and the run succeeds, because the item is still published under the same id.")]
    public async Task renamed_podcast_is_reported_and_allowed()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var previousKey = _fixture.CreateTitle().Replace(' ', '_').ToLowerInvariant();
        _scenario.WriteBaselineFile(previousKey, podcast.Id);
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);

        // Act
        var report = await _scenario.Publish(Baseline());

        // Assert
        var removal = report.Comparison!.Removals.Should().ContainSingle().Subject;
        removal.Reason.Should().Be(RemovalReason.Renamed);
        removal.Successor!.FileKey.Should().Be(podcast.FileKey);
        report.Comparison.Unexplained.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication baseline: when a previously published item is now flagged removed, then its file is reported " +
        "as removed and the run succeeds, because removing an item is a normal weekly change.")]
    public async Task removed_item_is_reported_and_allowed()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast(p => p.Removed = true);
        _scenario.WriteBaselineFile(podcast.FileKey, podcast.Id);

        // Act
        var report = await _scenario.Publish(Baseline());

        // Assert
        report.Comparison!.Removals.Should().ContainSingle().Which.Reason.Should().Be(RemovalReason.Removed);
        report.Comparison.Unexplained.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication baseline: when a previously published item no longer exists in Cosmos, then the run fails " +
        "naming the file and id, because a file vanishing without a removal or a migration is the silent " +
        "data loss this check exists to catch.")]
    public async Task vanished_item_fails_the_run()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var fileKey = _fixture.CreateTitle().Replace(' ', '_').ToLowerInvariant();
        _scenario.WriteBaselineFile(fileKey, id);

        // Act
        var run = () => _scenario.Publish(Baseline());

        // Assert
        var failure = await run.Should().ThrowAsync<PublicDatabaseConsistencyException>();
        failure.WithMessage($"*{fileKey}*").WithMessage($"*{id}*");
    }

    [Fact(DisplayName =
        "Publication baseline: when unexplained removals are explicitly accepted, then the run succeeds but " +
        "still reports them, because the operator has seen the list and chosen to publish the removal.")]
    public async Task accepted_unexplained_removals_are_still_reported()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var fileKey = _fixture.CreateTitle().Replace(' ', '_').ToLowerInvariant();
        _scenario.WriteBaselineFile(fileKey, id);

        // Act
        var report = await _scenario.Publish(Baseline() with { AcceptUnexplainedRemovals = true });

        // Assert
        var removal = report.Comparison!.Unexplained.Should().ContainSingle().Subject;
        removal.Reason.Should().Be(RemovalReason.NotFound);
        removal.FileKey.Should().Be(fileKey);
    }

    [Fact(DisplayName =
        "Publication baseline: when a previously published item still exists and is not removed but has nothing " +
        "to publish, then the run fails, because its file would disappear without anyone having removed it.")]
    public async Task live_item_without_content_fails_the_run()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.WriteBaselineFile(podcast.FileKey, podcast.Id);

        // Act
        var run = () => _scenario.Publish(Baseline());

        // Assert
        (await run.Should().ThrowAsync<PublicDatabaseConsistencyException>()).WithMessage($"*{podcast.FileKey}*");
    }

    [Fact(DisplayName =
        "Publication baseline: when a previously published file key is now published by a different item, then " +
        "the run fails, because the same file would silently change from one item to another in git history.")]
    public async Task file_key_taken_by_another_item_fails_the_run()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        _scenario.WriteBaselineFile(podcast.FileKey, _fixture.CreateGuid());

        // Act
        var run = () => _scenario.Publish(Baseline());

        // Assert
        (await run.Should().ThrowAsync<PublicDatabaseConsistencyException>()).WithMessage($"*{podcast.FileKey}*");
    }

    [Fact(DisplayName =
        "Publication baseline: when a baseline file has no readable id, then the run fails naming the file, " +
        "because the check cannot prove that file is still published.")]
    public async Task unreadable_baseline_file_fails_the_run()
    {
        // Arrange
        var fileKey = _fixture.CreateTitle().Replace(' ', '_').ToLowerInvariant();
        _scenario.WriteBaselineFile(fileKey, id: null);

        // Act
        var run = () => _scenario.Publish(Baseline());

        // Assert
        (await run.Should().ThrowAsync<PublicDatabaseConsistencyException>()).WithMessage($"*{fileKey}*");
    }

    [Fact(DisplayName =
        "Publication baseline: when files are unchanged or new, then they are counted rather than listed as " +
        "removals, because only files that stop being published need attention.")]
    public async Task unchanged_and_new_files_are_counted()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        _scenario.WriteBaselineFile(podcast.FileKey, podcast.Id);
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish(Baseline());

        // Assert
        report.Comparison!.BaselineFileCount.Should().Be(1);
        report.Comparison.Unchanged.Should().Be(1);
        report.Comparison.Added.Should().ContainSingle().Which.FileKey.Should().Be(film.FileKey);
        report.Comparison.Removals.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication baseline: when no baseline is supplied, then the run publishes as before and the report has " +
        "no comparison, because the baseline is how update.ps1 asks for removals to be checked.")]
    public async Task without_a_baseline_nothing_is_compared()
    {
        // Arrange
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Comparison.Should().BeNull();
        report.Published.Should().ContainSingle().Which.FileKey.Should().Be(film.FileKey);
    }

    private PublishOptions Baseline() => new(BaselinePath: _scenario.BaselineDirectory);
}
