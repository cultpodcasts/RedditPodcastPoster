using CatalogueMigrate;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Providers;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

public sealed class CatalogueMigrateEvidenceFileRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"catalogue-migrate-evidence-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact(DisplayName =
        "Catalogue migrate evidence: a batch file is JSON lines named by run, kind, and batch, and a begin snapshot reads back " +
        "with the same podcast and episode ids, because rollback restores from this file.")]
    public async Task file_batch_round_trips_snapshot()
    {
        // Arrange
        var sut = new FileCatalogueMigrateEvidenceStore(new JsonSerializerOptionsProvider());
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var runId = _fixture.CreateShortTitle().Replace(' ', '-');
        var record = new CatalogueMigrateEvidenceRecord(
            CatalogueMigrateEvidenceEvent.Begin,
            runId,
            RedditPodcastPoster.UrlSubmission.Categorisation.SubmitClassification.NewsReport,
            podcast.Id,
            podcast.Id,
            [episode.Id],
            DateTimeOffset.UtcNow,
            podcast,
            [episode]);

        // Act
        string name;
        await using (var batch = sut.OpenBatch(_directory, runId, record.Kind, 7))
        {
            name = batch.Name;
            await batch.Write(record);
            await batch.Write(record with { Event = CatalogueMigrateEvidenceEvent.Completed, BeforePodcast = null, BeforeEpisodes = null });
        }

        var read = await sut.Read(name).ToListAsync();

        // Assert
        Path.GetFileName(name).Should().Be($"{runId}-{record.Kind}-batch-007.jsonl");
        sut.Exists(name).Should().BeTrue();
        (await File.ReadAllLinesAsync(name)).Should().HaveCount(2);
        read.Select(r => r.Event).Should().Equal(CatalogueMigrateEvidenceEvent.Begin, CatalogueMigrateEvidenceEvent.Completed);
        read[0].BeforePodcast!.Id.Should().Be(podcast.Id);
        read[0].BeforePodcast!.Name.Should().Be(podcast.Name);
        read[0].BeforeEpisodes!.Single().Id.Should().Be(episode.Id);
        read[0].BeforeEpisodes!.Single().Title.Should().Be(episode.Title);
        read[1].BeforePodcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue migrate consent: the apply phrase names the kind and the rollback phrase names the evidence file, " +
        "and only an exact match with --apply counts, because consent must be specific and deliberate.")]
    public void consent_is_exact_and_specific()
    {
        // Arrange
        var kind = RedditPodcastPoster.UrlSubmission.Categorisation.SubmitClassification.Film;
        var file = $"{_fixture.CreateShortTitle().Replace(' ', '-')}.jsonl";

        // Act
        var exact = CatalogueMigrateConsent.IsApplyConsented(new CatalogueMigrateRequest
            { Kind = kind, Apply = true, Consent = CatalogueMigrateConsent.ApplyPhrase(kind) });
        var withoutApply = CatalogueMigrateConsent.IsApplyConsented(new CatalogueMigrateRequest
            { Kind = kind, Consent = CatalogueMigrateConsent.ApplyPhrase(kind) });
        var caseChanged = CatalogueMigrateConsent.IsApplyConsented(new CatalogueMigrateRequest
            { Kind = kind, Apply = true, Consent = CatalogueMigrateConsent.ApplyPhrase(kind).ToLowerInvariant() });
        var rollback = CatalogueMigrateConsent.IsRollbackConsented(new CatalogueMigrateRequest
            { Rollback = Path.Combine(_directory, file), Apply = true, Consent = CatalogueMigrateConsent.RollbackPhrase(file) });

        // Assert
        exact.Should().BeTrue();
        withoutApply.Should().BeFalse();
        caseChanged.Should().BeFalse();
        rollback.Should().BeTrue();
        CatalogueMigrateConsent.ApplyPhrase(kind).Should().Contain(kind);
        CatalogueMigrateConsent.RollbackPhrase(file).Should().Contain(file);
    }
}
