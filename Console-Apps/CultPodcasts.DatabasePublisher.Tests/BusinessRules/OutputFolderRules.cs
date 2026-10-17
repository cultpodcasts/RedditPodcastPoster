using System.Text.Json;
using CultPodcasts.DatabasePublisher.Consistency;
using CultPodcasts.DatabasePublisher.PublicModels;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Providers;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class OutputFolderRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "public-db-folder-" + Guid.NewGuid());

    public OutputFolderRules() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _scenario.Dispose();
        Directory.Delete(_folder, true);
    }

    [Fact(DisplayName =
        "Output folder: when the folder holds no published files and --initial was not given, then the run fails " +
        "naming --initial and writes nothing, because an empty folder is exactly what a forgotten previous " +
        "publish looks like and the removal check would otherwise pass over everything.")]
    public async Task empty_folder_without_initial_fails()
    {
        // Arrange
        _scenario.GivenFilm();

        // Act
        var run = () => _scenario.Publish();

        // Assert
        (await run.Should().ThrowAsync<PublicationFolderException>()).Which.Message.Should().Contain("--initial");
        _scenario.Files.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Output folder: when the folder is empty and --initial is given, then everything is created, because " +
        "that is how a genuine first publish says there is no previous one.")]
    public async Task initial_publish_creates_everything()
    {
        // Arrange
        var film = _scenario.GivenFilm();

        // Act
        var report = await _scenario.PublishFirstTime();

        // Assert
        report.Applied.Should().BeTrue();
        report.Plan.Files.Should().ContainSingle().Which.Mode.Should().Be(WriteMode.Create);
        _scenario.Files.Keys.Should().BeEquivalentTo(film.FileKey);
    }

    [Fact(DisplayName =
        "Output folder: when --initial is given but the folder already holds published files, then the run fails " +
        "and changes nothing, because --initial would switch off the removal check for a folder that has a history.")]
    public async Task initial_with_existing_files_fails()
    {
        // Arrange
        var fileKey = _scenario.GivenBaselineFileKey();
        _scenario.GivenExistingFile(fileKey, _fixture.CreateGuid());
        var before = _scenario.Files[fileKey];
        _scenario.GivenFilm();

        // Act
        var run = () => _scenario.PublishFirstTime();

        // Assert
        (await run.Should().ThrowAsync<PublicationFolderException>()).Which.Message.Should().Contain("--initial");
        _scenario.Files.Should().ContainSingle().Which.Value.Should().Be(before);
    }

    [Fact(DisplayName =
        "Output folder: when the folder does not exist, then reading it fails with the folder exception the program " +
        "turns into exit code 2, because that is a problem with the folder and not an unhandled crash.")]
    public async Task missing_folder_fails_with_the_folder_exception()
    {
        // Arrange
        var folder = new PublicationFolder(Path.Combine(_folder, _fixture.Create<string>()), new JsonSerializerOptionsProvider());

        // Act
        var read = () => folder.ReadBaselineAsync();

        // Assert
        (await read.Should().ThrowAsync<PublicationFolderException>()).Which.Message.Should().Contain("does not exist");
    }

    [Fact(DisplayName =
        "Output folder: when a podcast file and a prefixed file are read, then both ids are read and the episode " +
        "ids only from the podcast file, because the episode ids are only needed to recognise a podcast that " +
        "became a film.")]
    public async Task baseline_reads_ids_and_podcast_episode_ids_only()
    {
        // Arrange
        var podcastId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var showId = _fixture.CreateGuid();
        File.WriteAllText(Path.Combine(_folder, "a-podcast.json"), $$"""{ "id": "{{podcastId}}", "episodes": [ { "id": "{{episodeId}}" }, { "title": "no id" } ] }""");
        File.WriteAllText(Path.Combine(_folder, "tvshow-a-show.json"), $$"""{ "id": "{{showId}}", "episodes": [ { "id": "{{episodeId}}" } ] }""");
        File.WriteAllText(Path.Combine(_folder, "broken.json"), "{ not json");
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());

        // Act
        var baseline = await folder.ReadBaselineAsync();

        // Assert
        baseline.Select(b => (b.FileKey, b.Id, b.ChildIds.Count)).Should().Equal(
            ("a-podcast", podcastId, 1),
            ("broken", null, 0),
            ("tvshow-a-show", showId, 0));
    }

    [Fact(DisplayName =
        "Output folder: when a staged replacement is swapped in over a longer file, then no old content is left " +
        "behind and the staged file is gone, because the file must be exactly the new JSON.")]
    public async Task replacing_a_longer_file_leaves_no_trailing_content()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var podcast = new PublicPodcast(_fixture.CreateGuid()) { FileKey = "short", Name = _fixture.Create<string>() };
        File.WriteAllText(Path.Combine(_folder, "short.json"), new string('x', 10_000));
        await folder.StageAsync(podcast);

        // Act
        folder.Commit("short", WriteMode.Replace);

        // Assert
        var written = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_folder, "short.json")));
        written.RootElement.GetProperty("id").GetGuid().Should().Be(podcast.Id);
        folder.FindStaging().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Output folder: when staging a file, then only the .json.tmp file is written and the real file is not " +
        "touched, and staged files are not read as the baseline, because the folder is only changed when the plan " +
        "is applied.")]
    public async Task staging_writes_next_to_the_target_without_touching_it()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var podcast = new PublicPodcast(_fixture.CreateGuid()) { FileKey = "staged", Name = _fixture.Create<string>() };
        var original = _fixture.Create<string>();
        File.WriteAllText(Path.Combine(_folder, "staged.json"), original);

        // Act
        await folder.StageAsync(podcast);

        // Assert
        File.ReadAllText(Path.Combine(_folder, "staged.json")).Should().Be(original);
        folder.FindStaging().Should().Equal("staged.json.tmp");
        (await folder.ReadBaselineAsync()).Select(b => b.FileKey).Should().Equal("staged");
    }

    [Fact(DisplayName =
        "Output folder: when staged files are discarded, then they are all deleted and counted and the real files " +
        "stay, because discarding only clears what an unfinished run left.")]
    public void discarding_staging_deletes_only_the_staged_files()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        File.WriteAllText(Path.Combine(_folder, "a.json"), "{}");
        File.WriteAllText(Path.Combine(_folder, "a.json.tmp"), "{}");
        File.WriteAllText(Path.Combine(_folder, "b.json.tmp"), "{}");
        File.WriteAllText(Path.Combine(_folder, "notes.tmp"), "{}");

        // Act
        var discarded = folder.DiscardStaging();

        // Assert
        discarded.Should().Be(2);
        Directory.EnumerateFiles(_folder).Select(Path.GetFileName).Order().Should().Equal("a.json", "notes.tmp");
    }

    [Fact(DisplayName =
        "Output folder: when a swap is planned as a create but the file exists, or as a replace but it does not, " +
        "then it fails and the file is untouched, because only the planned files may be overwritten.")]
    public async Task commits_refuse_what_the_plan_did_not_say()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var existing = new PublicPodcast(_fixture.CreateGuid()) { FileKey = "existing" };
        var missing = new PublicPodcast(_fixture.CreateGuid()) { FileKey = "missing" };
        var original = _fixture.Create<string>();
        File.WriteAllText(Path.Combine(_folder, "existing.json"), original);
        await folder.StageAsync(existing);
        await folder.StageAsync(missing);

        // Act
        var createOverExisting = () => folder.Commit("existing", WriteMode.Create);
        var replaceMissing = () => folder.Commit("missing", WriteMode.Replace);

        // Assert
        createOverExisting.Should().Throw<InvalidOperationException>();
        replaceMissing.Should().Throw<InvalidOperationException>();
        File.ReadAllText(Path.Combine(_folder, "existing.json")).Should().Be(original);
        File.Exists(Path.Combine(_folder, "missing.json")).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Output folder: when the json files differ from the baseline the plan was made from, then the check fails " +
        "naming nothing was changed, because Cosmos is read for minutes and the folder must still be what was " +
        "planned from.")]
    public async Task folder_that_changed_since_the_baseline_is_refused()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        File.WriteAllText(Path.Combine(_folder, "a.json"), "{}");
        var baseline = await folder.ReadBaselineAsync();
        File.WriteAllText(Path.Combine(_folder, "b.json"), "{}");

        // Act
        var check = () => folder.EnsureUnchanged(baseline);

        // Assert
        check.Should().Throw<PublicationFolderException>().Which.Message.Should().Contain("nothing was changed");
    }

    [Fact(DisplayName =
        "Output folder: when a file key would leave the folder (a path separator), then staging it fails, because " +
        "a key from the database must never choose where on disk a file goes.")]
    public async Task file_keys_cannot_escape_the_folder()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());
        var escaping = new PublicPodcast(_fixture.CreateGuid()) { FileKey = Path.Combine("..", _fixture.Create<string>()) };

        // Act
        var stage = () => folder.StageAsync(escaping);

        // Assert
        await stage.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName =
        "Output folder: when a path is inside the folder, at any depth or the folder itself, then it is contained, " +
        "and a sibling whose name only starts the same is not, because the migration manifest must never land in " +
        "the data folder.")]
    public void containment_is_by_path_not_by_name_prefix()
    {
        // Arrange
        var folder = new PublicationFolder(_folder, new JsonSerializerOptionsProvider());

        // Act
        var inside = folder.Contains(Path.Combine(_folder, "a", "m.json"));
        var itself = folder.Contains(_folder);
        var sibling = folder.Contains(_folder + "-other" + Path.DirectorySeparatorChar + "m.json");

        // Assert
        (inside, itself, sibling).Should().Be((true, true, false));
    }
}
