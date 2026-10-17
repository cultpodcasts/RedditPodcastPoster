using CultPodcasts.DatabasePublisher.Consistency;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class PublicationPlanRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    [Fact(DisplayName =
        "Publication plan: when a podcast was transferred to a TV show, then its file is moved to the tvshow- " +
        "name and rewritten with the show, and the rename is logged, because git can only follow the history of " +
        "a file that was renamed, and the transfer keeps the podcast's id on the show.")]
    public async Task podcast_transferred_to_a_tv_show_is_moved_and_rewritten()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);
        _scenario.Podcasts.Clear();
        var show = _scenario.GivenTvShowFrom(podcast);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeTrue();
        var move = report.Plan.Moves.Should().ContainSingle().Subject;
        (move.FileKey, move.MoveTo, move.Reason).Should().Be((podcast.FileKey, show.FileKey, RemovalReason.Migrated));
        report.Plan.Files.Should().ContainSingle().Which.MovedFromKey.Should().Be(podcast.FileKey);
        _scenario.Files.Keys.Should().BeEquivalentTo(show.FileKey);
        IdOf(show.FileKey).Should().Be(show.Id);
        VerifyLogged(LogLevel.Information, $"Renamed '{podcast.FileKey}.json' to '{show.FileKey}.json'");
    }

    [Fact(DisplayName =
        "Publication plan: when a podcast was transferred to a news organisation, then its file is moved to the " +
        "news- name and rewritten with the organisation, because the transfer keeps the podcast's id on it.")]
    public async Task podcast_transferred_to_a_news_organisation_is_moved_and_rewritten()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);
        _scenario.Podcasts.Clear();
        var organisation = _scenario.GivenNewsOrganisationFrom(podcast);
        _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.Moves.Should().ContainSingle().Which.MoveTo.Should().Be(organisation.FileKey);
        _scenario.Files.Keys.Should().BeEquivalentTo(organisation.FileKey);
        IdOf(organisation.FileKey).Should().Be(organisation.Id);
    }

    [Fact(DisplayName =
        "Publication plan: when the only episode of a podcast became a film, then the podcast's file is moved to " +
        "the film- name and rewritten with the film, because the film takes the episode's id and that id is found " +
        "among the episodes inside the old file.")]
    public async Task podcast_whose_episode_became_a_film_is_moved_to_the_film_file()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 5);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id, episode.Id);
        _scenario.Podcasts.Clear();
        _scenario.Episodes.Clear();
        var film = _scenario.GivenFilmFrom(podcast, episode);

        // Act
        var report = await _scenario.Publish();

        // Assert
        film.Id.Should().Be(episode.Id);
        var move = report.Plan.Moves.Should().ContainSingle().Subject;
        (move.FileKey, move.MoveTo, move.Reason).Should().Be((podcast.FileKey, film.FileKey, RemovalReason.Migrated));
        report.Plan.Unexplained.Should().BeEmpty();
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey);
        IdOf(film.FileKey).Should().Be(film.Id);
    }

    [Fact(DisplayName =
        "Publication plan: when several episodes of one podcast became films, then the podcast's file is deleted " +
        "and every film is created, with no rename, because no single file can carry the history of more than one.")]
    public async Task podcast_whose_episodes_became_several_films_is_deleted_and_the_films_created()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var first = _scenario.GivenEpisode(podcast, releasedDaysAgo: 5);
        var second = _scenario.GivenEpisode(podcast, releasedDaysAgo: 6);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id, first.Id, second.Id);
        _scenario.Podcasts.Clear();
        _scenario.Episodes.Clear();
        var firstFilm = _scenario.GivenFilmFrom(podcast, first);
        var secondFilm = _scenario.GivenFilmFrom(podcast, second);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.Moves.Should().BeEmpty();
        var deletion = report.Plan.Deletions.Should().ContainSingle().Subject;
        deletion.Reason.Should().Be(RemovalReason.Migrated);
        deletion.Successors.Select(s => s.FileKey).Should().BeEquivalentTo(firstFilm.FileKey, secondFilm.FileKey);
        report.Plan.Unexplained.Should().BeEmpty();
        _scenario.Files.Keys.Should().BeEquivalentTo(firstFilm.FileKey, secondFilm.FileKey);
    }

    [Fact(DisplayName =
        "Publication plan: when a podcast changed its file key, then the old file is moved to the new name and " +
        "rewritten, because the item is the same and git should see one renamed file.")]
    public async Task renamed_podcast_is_moved_to_its_new_file_name()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        var previousKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(previousKey, podcast.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        var move = report.Plan.Moves.Should().ContainSingle().Subject;
        (move.FileKey, move.MoveTo, move.Reason).Should().Be((previousKey, podcast.FileKey, RemovalReason.Renamed));
        _scenario.Files.Keys.Should().BeEquivalentTo(podcast.FileKey);
    }

    [Fact(DisplayName =
        "Publication plan: when a migrated item's new file already exists next to its old one, then the old file " +
        "is deleted instead of being moved over it, because a move must never overwrite a file.")]
    public async Task old_file_is_deleted_when_the_new_file_already_exists()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);
        _scenario.Podcasts.Clear();
        var show = _scenario.GivenTvShowFrom(podcast);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);
        _scenario.GivenExistingFile(show.FileKey, show.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.Moves.Should().BeEmpty();
        report.Plan.Deletions.Should().ContainSingle().Which.FileKey.Should().Be(podcast.FileKey);
        _scenario.Files.Keys.Should().BeEquivalentTo(show.FileKey);
    }

    [Fact(DisplayName =
        "Publication plan: when an item still published under its key and id is written again, then its file is " +
        "replaced in place and nothing else changes, because that is the normal weekly update.")]
    public async Task unchanged_files_are_replaced_in_place()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.BaselineFileCount.Should().Be(1);
        report.Plan.Removals.Should().BeEmpty();
        report.Plan.Files.Select(f => (f.Item.FileKey, f.Mode)).Should().BeEquivalentTo(
            [(podcast.FileKey, WriteMode.Replace), (film.FileKey, WriteMode.Create)]);
        JsonNode.Parse(_scenario.Files[podcast.FileKey])!["episodes"]!.AsArray().Single()!["title"]!.GetValue<string>().Should().Be(episode.Title);
    }

    [Fact(DisplayName =
        "Publication plan: when a previously published item is now flagged removed, then its file is deleted and " +
        "the run succeeds, because removing an item is a normal weekly change.")]
    public async Task removed_item_has_its_file_deleted()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast(p => p.Removed = true);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.Deletions.Should().ContainSingle().Which.Reason.Should().Be(RemovalReason.Removed);
        report.Applied.Should().BeTrue();
        _scenario.Files.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication plan: when a previously published item no longer exists in Cosmos, then nothing is written, " +
        "moved or deleted and the plan names the file and id, because a file vanishing without a removal or a " +
        "migration is the silent data loss this check exists to catch.")]
    public async Task vanished_item_blocks_the_plan_and_touches_nothing()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var fileKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(fileKey, id);
        var before = _scenario.Files[fileKey];
        _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeFalse();
        report.Plan.IsBlocked.Should().BeTrue();
        var removal = report.Plan.Unexplained.Should().ContainSingle().Subject;
        (removal.FileKey, removal.Id, removal.Reason).Should().Be((fileKey, id, RemovalReason.NotFound));
        removal.Describe().Should().Contain(fileKey).And.Contain(id.ToString());
        _scenario.Files.Keys.Should().BeEquivalentTo(fileKey);
        _scenario.Files[fileKey].Should().Be(before);
    }

    [Fact(DisplayName =
        "Publication plan: when an unexplained removal is accepted by its file key, then that file is deleted, the " +
        "rest is published and the removal is still reported as accepted, because the operator has seen it.")]
    public async Task removal_accepted_by_file_key_is_applied_and_reported()
    {
        // Arrange
        var fileKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(fileKey, _fixture.CreateGuid());
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [fileKey.ToUpperInvariant()]));

        // Assert
        report.Applied.Should().BeTrue();
        var removal = report.Plan.Deletions.Should().ContainSingle().Subject;
        (removal.FileKey, removal.Reason, removal.Accepted).Should().Be((fileKey, RemovalReason.NotFound, true));
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey);
    }

    [Fact(DisplayName =
        "Publication plan: when an unexplained removal is accepted by its id, then it is applied, and a second " +
        "unexplained removal that was not accepted still blocks the plan, because accepting one reviewed file " +
        "must not switch the check off for the others.")]
    public async Task accepting_one_removal_does_not_accept_another()
    {
        // Arrange
        var acceptedId = _fixture.CreateGuid();
        _scenario.GivenExistingFile(_scenario.GivenBaselineFileKey(), acceptedId);
        var otherKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(otherKey, _fixture.CreateGuid());
        _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [acceptedId.ToString()]));

        // Assert
        report.Applied.Should().BeFalse();
        report.Plan.Unexplained.Should().ContainSingle().Which.FileKey.Should().Be(otherKey);
        _scenario.Files.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "Publication plan: when an --accept-removal value matches no unexplained removal, then the plan is " +
        "blocked and lists it, because a stale or mistyped acceptance must not be silently ignored.")]
    public async Task unused_acceptance_blocks_the_plan()
    {
        // Arrange
        var film = _scenario.GivenFilm();
        _scenario.GivenExistingFile(film.FileKey, film.Id);
        var typo = _fixture.Create<string>();

        // Act
        var report = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [typo]));

        // Assert
        report.Applied.Should().BeFalse();
        report.Plan.UnusedAcceptances.Should().Equal(typo);
        report.Plan.Unexplained.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication plan: when a previously published item still exists and is not removed but has no episodes, " +
        "then the plan is blocked, because its file would disappear without anyone having removed it.")]
    public async Task live_item_without_content_blocks_the_plan()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Plan.Unexplained.Should().ContainSingle().Which.Reason.Should().Be(RemovalReason.NoContent);
        report.Applied.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Publication plan: when a previously published file key is now published by a different item, then the " +
        "plan is blocked and nothing is overwritten, because the same file would silently change from one item to " +
        "another in git history.")]
    public async Task file_key_taken_by_another_item_blocks_the_plan()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        var otherId = _fixture.CreateGuid();
        _scenario.GivenExistingFile(podcast.FileKey, otherId);

        // Act
        var report = await _scenario.Publish();

        // Assert
        var removal = report.Plan.Unexplained.Should().ContainSingle().Subject;
        removal.Reason.Should().Be(RemovalReason.Replaced);
        removal.Successors.Single().Id.Should().Be(podcast.Id);
        IdOf(podcast.FileKey).Should().Be(otherId);
    }

    [Fact(DisplayName =
        "Publication plan: when a file in the output folder has no readable id, then the plan is blocked naming " +
        "the file, because the check cannot prove that file is still published.")]
    public async Task file_without_a_readable_id_blocks_the_plan()
    {
        // Arrange
        var fileKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(fileKey, id: null);
        _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish();

        // Assert
        var removal = report.Plan.Unexplained.Should().ContainSingle().Subject;
        (removal.FileKey, removal.Reason).Should().Be((fileKey, RemovalReason.Unreadable));
    }

    [Fact(DisplayName =
        "Publication plan: when the output folder holds files that are not top-level .json files, then they are " +
        "left alone, because only the planned files may ever be changed.")]
    public async Task other_files_in_the_folder_are_untouched()
    {
        // Arrange
        var film = _scenario.GivenFilm();
        var other = Path.Combine(_scenario.OutputDirectory, "README.md");
        var nested = Directory.CreateDirectory(Path.Combine(_scenario.OutputDirectory, "nested")).FullName;
        var nestedFile = Path.Combine(nested, "keep.json");
        var text = _fixture.Create<string>();
        File.WriteAllText(other, text);
        File.WriteAllText(nestedFile, text);

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        File.ReadAllText(other).Should().Be(text);
        File.ReadAllText(nestedFile).Should().Be(text);
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey);
    }

    [Fact(DisplayName =
        "Publication plan: when the publisher runs twice on one instance, then the second report holds only the " +
        "second run's files, because the publisher is a singleton and must not carry state between runs.")]
    public async Task a_second_run_reports_only_itself()
    {
        // Arrange
        _scenario.GivenFilm();
        _scenario.GivenFilm();
        var first = await _scenario.PublishFirstTime();
        _scenario.GivenFilm();

        // Act
        var second = await _scenario.Publish();

        // Assert
        first.Plan.Files.Should().HaveCount(2);
        second.Plan.Files.Should().HaveCount(3);
        second.Plan.Files.Count(f => f.Mode == WriteMode.Create).Should().Be(1);
        second.Plan.Removals.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Publication plan: when a vanished podcast file lists several episodes and only one of them became a film, " +
        "then it is unexplained and untouched and says how many episodes are not accounted for, because one " +
        "matching episode must not hide the others being dropped.")]
    public async Task podcast_file_where_only_one_episode_became_a_film_is_not_migrated()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var became = _scenario.GivenEpisode(podcast, releasedDaysAgo: 5);
        var other = _scenario.GivenEpisode(podcast, releasedDaysAgo: 6);
        var another = _scenario.GivenEpisode(podcast, releasedDaysAgo: 7);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id, became.Id, other.Id, another.Id);
        var before = _scenario.Files[podcast.FileKey];
        _scenario.Podcasts.Clear();
        _scenario.Episodes.Clear();
        _scenario.GivenFilmFrom(podcast, became);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeFalse();
        var removal = report.Plan.Unexplained.Should().ContainSingle().Subject;
        removal.Reason.Should().Be(RemovalReason.NotFound);
        removal.Describe().Should().Contain("only 1 of its 3 episodes").And.Contain("2 are not published anywhere");
        _scenario.Files.Should().ContainSingle().Which.Value.Should().Be(before);
    }

    [Fact(DisplayName =
        "Publication plan: when the partly migrated podcast file is accepted by name, then it is deleted and the " +
        "film is created, because the operator has been told how many episodes are not accounted for.")]
    public async Task partly_migrated_podcast_file_can_be_accepted_knowingly()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        var became = _scenario.GivenEpisode(podcast, releasedDaysAgo: 5);
        var other = _scenario.GivenEpisode(podcast, releasedDaysAgo: 6);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id, became.Id, other.Id);
        _scenario.Podcasts.Clear();
        _scenario.Episodes.Clear();
        var film = _scenario.GivenFilmFrom(podcast, became);

        // Act
        var report = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [podcast.FileKey]));

        // Assert
        report.Applied.Should().BeTrue();
        _scenario.FileNames.Should().Equal(film.FileKey + ".json");
        IdOf(film.FileKey).Should().Be(film.Id);
    }

    [Fact(DisplayName =
        "Publication plan: when two items swap file keys, then both old files are unexplained (each key is now " +
        "another item's) and nothing changes, and when both are accepted each key ends up holding its new item, " +
        "because a swap cannot be told from two unrelated replacements.")]
    public async Task swapped_file_keys_block_and_can_be_accepted_with_an_exact_end_state()
    {
        // Arrange
        var first = _scenario.GivenPodcast();
        _scenario.GivenEpisode(first, releasedDaysAgo: 1);
        var second = _scenario.GivenPodcast();
        _scenario.GivenEpisode(second, releasedDaysAgo: 2);
        _scenario.GivenExistingFile(first.FileKey, second.Id);
        _scenario.GivenExistingFile(second.FileKey, first.Id);

        // Act
        var blocked = await _scenario.Publish();
        var accepted = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [first.FileKey, second.FileKey]));

        // Assert
        blocked.Plan.Unexplained.Select(r => r.Reason).Should().Equal(RemovalReason.Replaced, RemovalReason.Replaced);
        accepted.Applied.Should().BeTrue();
        _scenario.Files.Keys.Should().BeEquivalentTo(first.FileKey, second.FileKey);
        IdOf(first.FileKey).Should().Be(first.Id);
        IdOf(second.FileKey).Should().Be(second.Id);
    }

    [Fact(DisplayName =
        "Publication plan: when one item moves to the key another existing file holds and that file's item moves " +
        "on (a chain), then the plan is blocked and untouched, because the file that is overwritten has no " +
        "explanation of its own.")]
    public async Task chain_of_renames_blocks_and_touches_nothing()
    {
        // Arrange
        var first = _scenario.GivenPodcast();
        _scenario.GivenEpisode(first, releasedDaysAgo: 1);
        var second = _scenario.GivenPodcast();
        _scenario.GivenEpisode(second, releasedDaysAgo: 2);
        var oldKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(oldKey, first.Id);
        _scenario.GivenExistingFile(first.FileKey, second.Id);
        var before = _scenario.Files;

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeFalse();
        var unexplained = report.Plan.Unexplained.Should().ContainSingle().Subject;
        (unexplained.FileKey, unexplained.Reason).Should().Be((first.FileKey, RemovalReason.Replaced));
        _scenario.Files.Should().BeEquivalentTo(before);
    }

    [Fact(DisplayName =
        "Publication plan: when two existing files carry the same id and the item now has another key, then the " +
        "first of them (by name) is moved to it and the second is deleted, because a target can be claimed once.")]
    public async Task two_files_claiming_one_target_move_one_and_delete_the_other()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        var keys = new[] { _scenario.GivenBaselineFileKey(), _scenario.GivenBaselineFileKey() }
            .Order(StringComparer.Ordinal).ToList();
        _scenario.GivenExistingFile(keys[0], podcast.Id);
        _scenario.GivenExistingFile(keys[1], podcast.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        var move = report.Plan.Moves.Should().ContainSingle().Subject;
        (move.FileKey, move.MoveTo).Should().Be((keys[0], podcast.FileKey));
        report.Plan.Deletions.Should().ContainSingle().Which.FileKey.Should().Be(keys[1]);
        _scenario.FileNames.Should().Equal(podcast.FileKey + ".json");
    }

    [Fact(DisplayName =
        "Publication plan: when an item's key differs from its existing file's key only by case, then the old file " +
        "is deleted and the new one created under the exact new name, because on a case-sensitive file system a " +
        "replace of the differently-cased path would not exist and on a case-insensitive one the old case would stay.")]
    public async Task key_that_differs_only_by_case_is_deleted_and_created()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        podcast.FileKey = char.ToUpperInvariant(podcast.FileKey[0]) + podcast.FileKey[1..];
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        var lower = podcast.FileKey.ToLowerInvariant();
        lower.Should().NotBe(podcast.FileKey);
        _scenario.GivenExistingFile(lower, podcast.Id);

        // Act
        var report = await _scenario.Publish();

        // Assert
        report.Applied.Should().BeTrue();
        report.Plan.Moves.Should().BeEmpty();
        report.Plan.Deletions.Should().ContainSingle().Which.FileKey.Should().Be(lower);
        _scenario.FileNames.Should().Equal(podcast.FileKey + ".json");
        IdOf(podcast.FileKey).Should().Be(podcast.Id);
    }

    [Fact(DisplayName =
        "Publication plan: when an --accept-removal value names a file that is already explained (a migrated " +
        "podcast), then the plan is blocked as an unused acceptance, because acceptances are only for removals " +
        "that were not explained.")]
    public async Task accepting_an_explained_removal_blocks_as_unused()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast(p => p.Removed = true);
        _scenario.GivenExistingFile(podcast.FileKey, podcast.Id);

        // Act
        var report = await _scenario.Publish(new PublishOptions(AcceptedRemovals: [podcast.FileKey]));

        // Assert
        report.Applied.Should().BeFalse();
        report.Plan.UnusedAcceptances.Should().Equal(podcast.FileKey);
        _scenario.Files.Keys.Should().BeEquivalentTo(podcast.FileKey);
    }

    private Guid IdOf(string fileKey) => JsonNode.Parse(_scenario.Files[fileKey])!["id"]!.GetValue<Guid>();
    private void VerifyLogged(LogLevel level, string messageFragment) =>
        _scenario.Logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(messageFragment)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
}
