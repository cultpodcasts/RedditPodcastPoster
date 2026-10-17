using CultPodcasts.DatabasePublisher.Consistency;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class StagedPublishRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    [Fact(DisplayName =
        "Staged publish: when reading an item's content from Cosmos fails after the plan was made, then the " +
        "exception surfaces, no existing file is deleted, moved or changed and no staged file is left, because " +
        "all content is built before the folder is touched.")]
    public async Task cosmos_failure_leaves_the_folder_exactly_as_it_was()
    {
        // Arrange
        var removed = _scenario.GivenPodcast(p => p.Removed = true);
        _scenario.GivenExistingFile(removed.FileKey, removed.Id);
        var show = _scenario.GivenTvShow();
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);
        _scenario.GivenExistingFile(show.FileKey, show.Id);
        var before = _scenario.Files;
        _scenario.CosmosFailure = new InvalidOperationException(_fixture.Create<string>());

        // Act
        var run = () => _scenario.Publish();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>();
        _scenario.Files.Should().BeEquivalentTo(before);
        _scenario.StagedFileNames.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Staged publish: when a swap fails part-way through applying, then the failure is reported as an apply " +
        "failure and the unswapped staged files stay, because the folder may be partly updated and that must " +
        "not be mistaken for a clean one.")]
    public async Task failure_while_applying_is_reported_and_leaves_the_staged_files()
    {
        // Arrange
        var first = _scenario.GivenFilm();
        var second = _scenario.GivenFilm();
        _scenario.FailCommitNumber(2);

        // Act
        var run = () => _scenario.PublishFirstTime();

        // Assert
        (await run.Should().ThrowAsync<PublicationApplyException>()).Which.InnerException.Should().BeOfType<IOException>();
        var keys = new[] { first.FileKey, second.FileKey }.Order(StringComparer.Ordinal).ToList();
        _scenario.FileNames.Should().Equal(keys[0] + ".json");
        _scenario.StagedFileNames.Should().Equal(keys[1] + ".json.tmp");
    }

    [Fact(DisplayName =
        "Staged publish: when staged files are left from a run that did not finish, then the next run refuses and " +
        "names --discard-staging and changes nothing, because the folder may be partly updated and a person " +
        "should look first.")]
    public async Task leftover_staged_files_refuse_the_next_run_untouched()
    {
        // Arrange
        var film = _scenario.GivenFilm();
        var existing = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(existing, _fixture.CreateGuid());
        File.WriteAllText(Path.Combine(_scenario.OutputDirectory, film.FileKey + ".json.tmp"), "{}");
        var before = _scenario.Files;

        // Act
        var run = () => _scenario.Publish(new PublishOptions(AcceptedRemovals: [existing]));

        // Assert
        (await run.Should().ThrowAsync<PublicationFolderException>()).Which.Message
            .Should().Contain("--discard-staging").And.Contain(film.FileKey + ".json.tmp");
        _scenario.Files.Should().BeEquivalentTo(before);
        _scenario.StagedFileNames.Should().Equal(film.FileKey + ".json.tmp");
    }

    [Fact(DisplayName =
        "Staged publish: when a run failed part-way and the next run is given --discard-staging, then the staged " +
        "files are deleted and the publish completes with every file complete and nothing staged left, because " +
        "the plan is made again from whatever the folder now holds.")]
    public async Task discarding_staging_recovers_a_run_that_failed_part_way()
    {
        // Arrange
        var removed = _scenario.GivenPodcast(p => p.Removed = true);
        _scenario.GivenExistingFile(removed.FileKey, removed.Id);
        var first = _scenario.GivenFilm();
        var second = _scenario.GivenFilm();
        _scenario.FailCommitNumber(2);
        var failed = () => _scenario.Publish();
        await failed.Should().ThrowAsync<PublicationApplyException>();
        _scenario.StopFailingCommits();

        // Act
        var report = await _scenario.Publish(new PublishOptions(DiscardStaging: true));

        // Assert
        report.Applied.Should().BeTrue();
        _scenario.Files.Keys.Should().BeEquivalentTo(first.FileKey, second.FileKey);
        _scenario.StagedFileNames.Should().BeEmpty();
        IdOf(first.FileKey).Should().Be(first.Id);
        IdOf(second.FileKey).Should().Be(second.Id);
    }

    [Fact(DisplayName =
        "Staged publish: when --discard-staging is given and nothing is staged, then the run is the same as any " +
        "other, because the flag only clears leftovers.")]
    public async Task discard_staging_with_nothing_staged_is_harmless()
    {
        // Arrange
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish(new PublishOptions(Initial: true, DiscardStaging: true));

        // Assert
        report.Applied.Should().BeTrue();
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey);
    }

    [Fact(DisplayName =
        "Staged publish: when the plan is blocked, then nothing is staged and nothing changes, because staging " +
        "belongs to applying a plan.")]
    public async Task blocked_plan_stages_nothing()
    {
        // Arrange
        var existing = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(existing, _fixture.CreateGuid());
        _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeFalse();
        _scenario.FileNames.Should().Equal(existing + ".json");
        _scenario.StagedFileNames.Should().BeEmpty();
    }

    private Guid IdOf(string fileKey) =>
        System.Text.Json.Nodes.JsonNode.Parse(_scenario.Files[fileKey])!["id"]!.GetValue<Guid>();

    [Fact(DisplayName =
        "Staged publish: when the output folder does not exist, then the run fails with the folder exception " +
        "(exit 2) before anything else, because a missing folder is a folder problem and not an unexpected failure.")]
    public async Task missing_output_folder_fails_with_the_folder_exception()
    {
        // Arrange
        _scenario.GivenFilm();
        Directory.Delete(_scenario.OutputDirectory);

        // Act
        var run = () => _scenario.PublishFirstTime();

        // Assert
        (await run.Should().ThrowAsync<PublicationFolderException>()).Which.Message.Should().Contain("does not exist");
    }

    [Fact(DisplayName =
        "Staged publish: when a run failed part-way after deleting an accepted file and the same --accept-removal " +
        "is repeated, then the re-run is blocked as an unused acceptance and changes nothing, because that file is " +
        "already gone and the stale value must be dropped.")]
    public async Task repeating_an_acceptance_for_a_file_the_first_run_deleted_is_blocked()
    {
        // Arrange
        var gone = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(gone, _fixture.CreateGuid());
        var film = _scenario.GivenFilm();
        var kept = _scenario.GivenFilm();
        _scenario.GivenExistingFile(kept.FileKey, kept.Id);
        var options = new PublishOptions(AcceptedRemovals: [gone]);
        _scenario.FailCommitNumber(1);
        var failed = () => _scenario.Publish(options);
        await failed.Should().ThrowAsync<PublicationApplyException>();
        _scenario.StopFailingCommits();

        // Act
        var report = await _scenario.Publish(options with { DiscardStaging = true });
        var withoutIt = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeFalse();
        report.Plan.UnusedAcceptances.Should().Equal(gone);
        withoutIt.Applied.Should().BeTrue();
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey, kept.FileKey);
    }
}
