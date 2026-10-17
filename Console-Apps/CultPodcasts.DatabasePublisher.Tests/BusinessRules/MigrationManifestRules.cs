using System.Text.Json.Nodes;
using CultPodcasts.DatabasePublisher.Consistency;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class MigrationManifestRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();
    private readonly string _manifestFolder = Path.Combine(Path.GetTempPath(), "public-db-manifest-" + Guid.NewGuid());

    public void Dispose()
    {
        _scenario.Dispose();
        if (Directory.Exists(_manifestFolder))
        {
            Directory.Delete(_manifestFolder, true);
        }
    }

    private string ManifestPath => Path.Combine(_manifestFolder, "nested", "migrations.json");

    [Fact(DisplayName =
        "Migration manifest: when a podcast became a TV show, a podcast was renamed and a podcast's only episode " +
        "became a film, then --migrations-out lists each with its old file, new file, kind, reason and ids, because " +
        "a tool after the publish needs to check what git made of every one of them.")]
    public async Task manifest_lists_every_move_with_old_and_new_file()
    {
        // Arrange
        var transferred = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(transferred.FileKey, transferred.Id);
        _scenario.Podcasts.Remove(transferred);
        var show = _scenario.GivenTvShowFrom(transferred);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);

        var renamed = _scenario.GivenPodcast();
        _scenario.GivenEpisode(renamed, releasedDaysAgo: 1);
        var previousKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(previousKey, renamed.Id);

        var source = _scenario.GivenPodcast();
        var episode = _scenario.GivenEpisode(source, releasedDaysAgo: 2);
        _scenario.GivenExistingFile(source.FileKey, source.Id, episode.Id);
        _scenario.Podcasts.Remove(source);
        _scenario.Episodes.Remove(episode);
        var film = _scenario.GivenFilmFrom(source, episode);

        // Act
        var report = await _scenario.Publish(new PublishOptions(MigrationsOut: ManifestPath));

        // Assert
        report.Applied.Should().BeTrue();
        report.MigrationsFile.Should().Be(Path.GetFullPath(ManifestPath));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(ManifestPath))!;
        manifest["version"]!.GetValue<int>().Should().Be(1);
        var entries = manifest["migrations"]!.AsArray()
            .Select(e => (
                Old: e!["oldFile"]!.GetValue<string>(),
                New: e["newFile"]!.GetValue<string>(),
                Kind: e["kind"]!.GetValue<string>(),
                Reason: e["reason"]!.GetValue<string>(),
                Id: e["id"]!.GetValue<Guid>(),
                NewId: e["newId"]!.GetValue<Guid>()))
            .ToList();
        entries.Should().BeEquivalentTo(new[]
        {
            (transferred.FileKey + ".json", show.FileKey + ".json", "tvShow", "migrated", transferred.Id, show.Id),
            (previousKey + ".json", renamed.FileKey + ".json", "podcast", "renamed", renamed.Id, renamed.Id),
            (source.FileKey + ".json", film.FileKey + ".json", "film", "migrated", source.Id, film.Id)
        }.Select(e => (Old: e.Item1, New: e.Item2, Kind: e.Item3, Reason: e.Item4, Id: e.Item5, NewId: e.Item6)));
        entries.Select(e => e.Old).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact(DisplayName =
        "Migration manifest: when nothing was moved, then --migrations-out still writes the manifest with an " +
        "empty list, because a missing file must mean the publish did not complete.")]
    public async Task manifest_is_written_empty_when_there_were_no_migrations()
    {
        // Arrange
        var film = _scenario.GivenFilm();
        _scenario.GivenExistingFile(film.FileKey, film.Id);

        // Act
        await _scenario.Publish(new PublishOptions(MigrationsOut: ManifestPath));

        // Assert
        JsonNode.Parse(await File.ReadAllTextAsync(ManifestPath))!["migrations"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Migration manifest: when --migrations-out is not given, then no manifest is written anywhere in the " +
        "output folder, because it must never be published with the data.")]
    public async Task no_manifest_is_written_without_the_option()
    {
        // Arrange
        _scenario.GivenFilm();

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        Directory.EnumerateFileSystemEntries(_scenario.OutputDirectory).Select(p => Path.GetExtension(p)).Should().OnlyContain(e => e == ".json");
    }

    [Fact(DisplayName =
        "Migration manifest: when the plan is blocked, then no manifest is written, because the folder was not " +
        "published.")]
    public async Task blocked_plan_writes_no_manifest()
    {
        // Arrange
        var existing = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(existing, _fixture.CreateGuid());
        _scenario.GivenFilm();

        // Act
        var report = await _scenario.Publish(new PublishOptions(MigrationsOut: ManifestPath));

        // Assert
        report.Applied.Should().BeFalse();
        File.Exists(ManifestPath).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Migration manifest: when --migrations-out points inside the output folder, then the run fails before " +
        "anything is changed or written, because a manifest in the data folder could be committed with the data.")]
    public async Task manifest_inside_the_output_folder_is_refused_before_anything_changes()
    {
        // Arrange
        _scenario.GivenFilm();
        var inside = Path.Combine(_scenario.OutputDirectory, "sub", "migrations.json");

        // Act
        var run = () => _scenario.PublishFirstTime(new PublishOptions(Initial: true, MigrationsOut: inside));

        // Assert
        (await run.Should().ThrowAsync<PublicationFolderException>()).Which.Message.Should().Contain("--migrations-out");
        Directory.EnumerateFileSystemEntries(_scenario.OutputDirectory).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Migration manifest: when the --migrations-out path cannot be written, then the run fails naming the path " +
        "before the folder is touched, and no existing file changes and nothing stays staged, because the list of " +
        "migrations must not be lost after the folder has been published.")]
    public async Task unwritable_manifest_path_fails_before_the_folder_is_touched()
    {
        // Arrange
        var transferred = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(transferred.FileKey, transferred.Id);
        _scenario.Podcasts.Remove(transferred);
        var show = _scenario.GivenTvShowFrom(transferred);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);
        Directory.CreateDirectory(_manifestFolder);
        var blocker = Path.Combine(_manifestFolder, "blocker");
        File.WriteAllText(blocker, _fixture.Create<string>());
        var unwritable = Path.Combine(blocker, "migrations.json");
        var before = _scenario.Files;

        // Act
        var run = () => _scenario.Publish(new PublishOptions(MigrationsOut: unwritable));

        // Assert
        (await run.Should().ThrowAsync<IOException>()).Which.Message.Should().Contain(unwritable);
        _scenario.Files.Should().BeEquivalentTo(before);
        _scenario.StagedFileNames.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Migration manifest: when the publish fails part-way while applying, then the manifest of what the plan " +
        "intended is already on disk, because it is written before the folder is changed.")]
    public async Task manifest_of_the_plan_survives_a_failed_apply()
    {
        // Arrange
        var transferred = _scenario.GivenPodcast();
        _scenario.GivenExistingFile(transferred.FileKey, transferred.Id);
        _scenario.Podcasts.Remove(transferred);
        var show = _scenario.GivenTvShowFrom(transferred);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);
        _scenario.FailCommitNumber(1);

        // Act
        var run = () => _scenario.Publish(new PublishOptions(MigrationsOut: ManifestPath));

        // Assert
        await run.Should().ThrowAsync<PublicationApplyException>();
        var migration = JsonNode.Parse(await File.ReadAllTextAsync(ManifestPath))!["migrations"]!.AsArray().Single()!;
        (migration["oldFile"]!.GetValue<string>(), migration["newFile"]!.GetValue<string>())
            .Should().Be((transferred.FileKey + ".json", show.FileKey + ".json"));
    }
}
