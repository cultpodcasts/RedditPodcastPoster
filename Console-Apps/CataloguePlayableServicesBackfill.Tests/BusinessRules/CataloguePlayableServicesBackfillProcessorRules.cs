using System.Runtime.CompilerServices;
using System.Text.Json;
using CataloguePlayableServicesBackfill;
using CataloguePlayableServicesBackfill.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class CataloguePlayableServicesBackfillProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeBackupStore _backups = new();
    private readonly AutoMocker _mocker;
    private readonly string _tempDir;

    public CataloguePlayableServicesBackfillProcessorRules()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cpsb-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _mocker = new AutoMocker();
        _mocker.Use(_backups);
        _mocker.Use<IEpisodeBackupStore>(_backups);
        _mocker.Use(NullLogger<CataloguePlayableServicesBackfillProcessor>.Instance);
        _mocker.Use<TimeProvider>(new FixedTimeProvider(DateTimeOffset.Parse("2026-10-26T00:00:00Z")));
        _mocker.Use<Func<IPlayableSearchDocumentSwap>>(() => _mocker.Get<IPlayableSearchDocumentSwap>());

        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(EmptyAsync<Film>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAll())
            .Returns(EmptyAsync<RedditPodcastPoster.Models.TvShows.TvShowEpisode>());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAll())
            .Returns(EmptyAsync<RedditPodcastPoster.Models.News.NewsReport>());
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --apply is set without --consent, then the tool exits 2 " +
        "without scanning, because writes need the exact human consent phrase.")]
    public async Task apply_without_consent_is_refused()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            Apply = true,
            ChangeLog = Path.Combine(_tempDir, "refused.jsonl")
        });

        // Assert
        result.ExitCode.Should().Be(CataloguePlayableServicesBackfillProcessor.ExitApplyRefused);
        result.Scanned.Should().Be(0);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.GetAll(), Times.Never);
        _mocker.GetMock<IFilmRepository>()
            .Verify(r => r.Update(It.IsAny<Guid>(), It.IsAny<Func<Film, bool>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a film has no episode backup, then dry-run counts missing backup " +
        "and writes a change-log line, because the GUID join failed.")]
    public async Task missing_backup_is_logged()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "missing.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.MissingBackup.Should().Be(1);
        result.Discrepancies.Should().Be(0);
        var line = (await File.ReadAllLinesAsync(changeLog)).Should().ContainSingle().Subject;
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(line, BackupDocumentJsonOptions.Instance);
        record!.MissingBackup.Should().BeTrue();
        record.Id.Should().Be(film.Id);
        record.Applied.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when backup and film services match, then dry-run counts identical " +
        "and does not append a discrepancy change-log line.")]
    public async Task identical_is_skipped()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = url }
        };
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = PlayableServicesDiscrepancyCarry.CloneServices(services) };
        _backups.ById[id] = PlayableServicesDiscrepancyCarry.CloneServices(services)!;
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "identical.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.Identical.Should().Be(1);
        result.Discrepancies.Should().Be(0);
        File.Exists(changeLog).Should().BeTrue();
        (await File.ReadAllLinesAsync(changeLog)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when backup has a url the film lacks, then dry-run records a discrepancy " +
        "change-log line with applied false and does not call Update.")]
    public async Task discrepancy_is_logged_on_dry_run()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var backupUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = null };
        _backups.ById[id] = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = backupUrl }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "discrepancy.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.Discrepancies.Should().Be(1);
        result.Applied.Should().Be(0);
        var line = (await File.ReadAllLinesAsync(changeLog)).Should().ContainSingle().Subject;
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(line, BackupDocumentJsonOptions.Instance)!;
        record.Applied.Should().BeFalse();
        record.Kind.Should().Be("film");
        record.Id.Should().Be(id);
        record.Changes.Should().ContainSingle(c => c.Field == "url" && c.After == backupUrl.AbsoluteUri);
        _mocker.GetMock<IFilmRepository>()
            .Verify(r => r.Update(It.IsAny<Guid>(), It.IsAny<Func<Film, bool>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --apply has consent but Cosmos Update returns null, " +
        "then the run exits 3 with FailedWrites counted, because operators must see write failures in $LASTEXITCODE.")]
    public async Task failed_write_exits_non_zero()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var backupUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = null };
        _backups.ById[id] = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = backupUrl }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Update(id, It.IsAny<Func<Film, bool>>()))
            .ReturnsAsync((Film?)null);
        var changeLog = Path.Combine(_tempDir, "failed-write.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            Apply = true,
            Consent = CataloguePlayableServicesBackfillConsent.ApplyPhrase,
            ChangeLog = changeLog
        });

        // Assert
        result.ExitCode.Should().Be(CataloguePlayableServicesBackfillProcessor.ExitFailed);
        result.FailedWrites.Should().Be(1);
        result.Applied.Should().Be(0);
        result.Discrepancies.Should().Be(1);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.Applied.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --apply has the exact consent phrase and a discrepancy exists, " +
        "then Update runs and the change-log marks applied true.")]
    public async Task apply_with_consent_writes()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var backupUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = null };
        _backups.ById[id] = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = backupUrl }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Update(id, It.IsAny<Func<Film, bool>>()))
            .ReturnsAsync((Guid _, Func<Film, bool> mutate) =>
            {
                mutate(film);
                return film;
            });
        var changeLog = Path.Combine(_tempDir, "apply.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            Apply = true,
            Consent = CataloguePlayableServicesBackfillConsent.ApplyPhrase,
            ChangeLog = changeLog
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.Applied.Should().Be(1);
        film.Services![ServiceKeys.Spotify].Url.Should().Be(backupUrl);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.Applied.Should().BeTrue();
        _mocker.GetMock<IFilmRepository>()
            .Verify(r => r.Update(id, It.IsAny<Func<Film, bool>>()), Times.Once);
    }

    private static async IAsyncEnumerable<T> EmptyAsync<T>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(
        T item,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return item;
    }
}
