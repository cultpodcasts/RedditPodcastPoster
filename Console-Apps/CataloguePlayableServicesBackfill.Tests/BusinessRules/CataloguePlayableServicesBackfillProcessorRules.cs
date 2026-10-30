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
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class CataloguePlayableServicesBackfillProcessorRules : IDisposable
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
        _mocker.Use<Func<string, IPublicDatabaseUrlIndex>>(dir => FilePublicDatabaseUrlIndex.Load(dir));

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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
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
        "Catalogue playable services backfill: when a film has no episode backup and no public urls, then dry-run " +
        "counts missing truth and writes a change-log line, because neither truth source had the GUID.")]
    public async Task missing_truth_is_logged()
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
        result.MissingTruth.Should().Be(1);
        result.Discrepancies.Should().Be(0);
        var line = (await File.ReadAllLinesAsync(changeLog)).Should().ContainSingle().Subject;
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(line, BackupDocumentJsonOptions.Instance);
        record!.MissingTruth.Should().BeTrue();
        record.Id.Should().Be(film.Id);
        record.Applied.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when episode backup is missing but PublicDatabase has the film url, " +
        "then dry-run records a discrepancy from the public urls, because public download is a second truth source.")]
    public async Task public_database_fills_missing_episode_backup()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var publicUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = null };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var publicDir = Path.Combine(_tempDir, "public");
        Directory.CreateDirectory(publicDir);
        await File.WriteAllTextAsync(
            Path.Combine(publicDir, "film.json"),
            JsonSerializer.Serialize(new
            {
                id,
                name = _fixture.CreateTitle(),
                urls = new Dictionary<string, string>
                {
                    [ServiceKeys.YouTube] = publicUrl.AbsoluteUri
                }
            }));
        var changeLog = Path.Combine(_tempDir, "public-fill.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            PublicDatabaseDir = publicDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.MissingTruth.Should().Be(0);
        result.Discrepancies.Should().Be(1);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.MissingTruth.Should().BeFalse();
        record.PublicDatabasePath.Should().EndWith("film.json");
        record.Changes.Should().ContainSingle(c => c.Field == "url" && c.After == publicUrl.AbsoluteUri);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --public-database-dir alone is set (no episode backup dir), " +
        "then dry-run records a discrepancy from public urls, because public download is a supported sole truth source.")]
    public async Task public_database_dir_alone_records_discrepancy()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var publicUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var film = new Film(_fixture.CreateTitle()) { Id = id, Services = null };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var publicDir = Path.Combine(_tempDir, "public-alone");
        Directory.CreateDirectory(publicDir);
        await File.WriteAllTextAsync(
            Path.Combine(publicDir, "film.json"),
            JsonSerializer.Serialize(new
            {
                id,
                name = _fixture.CreateTitle(),
                urls = new Dictionary<string, string>
                {
                    [ServiceKeys.YouTube] = publicUrl.AbsoluteUri
                }
            }));
        var changeLog = Path.Combine(_tempDir, "public-alone.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = null,
            PublicDatabaseDir = publicDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.MissingTruth.Should().Be(0);
        result.Discrepancies.Should().Be(1);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.PublicDatabasePath.Should().EndWith("film.json");
        record.Changes.Should().ContainSingle(c => c.Field == "url" && c.After == publicUrl.AbsoluteUri);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when neither --episode-backup-dir nor --public-database-dir is set, " +
        "then the tool exits 1 without scanning, because at least one truth source is required.")]
    public async Task missing_truth_dirs_is_bad_arguments()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            Kind = "film",
            ChangeLog = Path.Combine(_tempDir, "no-dirs.jsonl")
        });

        // Assert
        result.ExitCode.Should().Be(CataloguePlayableServicesBackfillProcessor.ExitBadArguments);
        result.Scanned.Should().Be(0);
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
        "Catalogue playable services backfill: when services already match backup but YouTubeId is missing while " +
        "services.youtube.url is present, then dry-run counts a discrepancy so compact youTubeId can be persisted for search.")]
    public async Task missing_youtube_id_with_matching_services_is_discrepancy()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var videoId = _fixture.CreateYouTubeId();
        var youTubeUrl = _fixture.DefaultYouTubeUrl(videoId);
        var services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = youTubeUrl }
        };
        var film = new Film(_fixture.CreateTitle())
        {
            Id = id,
            Services = PlayableServicesDiscrepancyCarry.CloneServices(services),
            YouTubeId = null
        };
        _backups.ById[id] = PlayableServicesDiscrepancyCarry.CloneServices(services)!;
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "youtube-id.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.Identical.Should().Be(0);
        result.Discrepancies.Should().Be(1);
        film.YouTubeId.Should().Be(videoId);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.Changes.Should().ContainSingle(c => c.Field == "youTubeId");
        record.Applied.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a service url is carried and YouTubeId also syncs from an " +
        "existing services.youtube.url, then the change-log includes both so apply logs stay auditable.")]
    public async Task services_and_youtube_id_changes_both_appear_in_change_log()
    {
        // Arrange — live already has YouTube URL (so SyncYouTubeIdIfNeeded fires) but lacks Spotify;
        // backup supplies Spotify. Compact YouTubeId is missing on the film.
        var id = _fixture.CreateGuid();
        var videoId = _fixture.CreateYouTubeId();
        var youTubeUrl = _fixture.DefaultYouTubeUrl(videoId);
        var spotifyUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        var liveServices = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = youTubeUrl }
        };
        var film = new Film(_fixture.CreateTitle())
        {
            Id = id,
            Services = PlayableServicesDiscrepancyCarry.CloneServices(liveServices),
            YouTubeId = null
        };
        _backups.ById[id] = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = youTubeUrl },
            [ServiceKeys.Spotify] = new() { Url = spotifyUrl }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "services-and-youtube-id.jsonl");
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
        film.YouTubeId.Should().Be(videoId);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.Changes.Should().Contain(c => c.Field == "url" && c.Key == ServiceKeys.Spotify);
        record.Changes.Should().Contain(c => c.Field == "youTubeId");
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

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a news report has services.youtube.url but no YouTubeId and " +
        "neither truth source has its GUID, then dry-run still counts a youTubeId discrepancy instead of missing " +
        "truth, because the compact id comes from the live url and needs no backup.")]
    public async Task missing_truth_still_syncs_youtube_id()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = _fixture.CreateGuid(),
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            },
            YouTubeId = null
        };
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(report));
        var changeLog = Path.Combine(_tempDir, "missing-truth-youtube-id.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            EpisodeBackupDir = _tempDir,
            Kind = "newsreport",
            ChangeLog = changeLog
        });

        // Assert
        result.MissingTruth.Should().Be(0);
        result.Discrepancies.Should().Be(1);
        report.YouTubeId.Should().Be(videoId);
        var record = JsonSerializer.Deserialize<ChangeLogRecord>(
            (await File.ReadAllLinesAsync(changeLog)).Single(),
            BackupDocumentJsonOptions.Instance)!;
        record.MissingTruth.Should().BeFalse();
        record.YouTubeIdOutcome.Should().Be(CataloguePlayableServicesBackfillProcessor.YouTubeIdFilled);
        record.Changes.Should().ContainSingle().Which.Should()
            .Be(new ServiceFieldChange(ServiceKeys.YouTube, "youTubeId", null, videoId));
        result.YouTubeIdFilled.Should().Be(1);
        result.YouTubeIdOverwritten.Should().Be(0);
        result.YouTubeIdCleared.Should().Be(0);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.PatchFields(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CataloguePlayableFieldPatch>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --youtube-id-only runs with no truth directories and consent, " +
        "then a url-only news report is saved with its YouTubeId and its search document is pushed with youtubeId, " +
        "because the id sync needs no backup or PublicDatabase.")]
    public async Task youtube_id_only_applies_without_truth_dirs()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var youTubeUrl = _fixture.DefaultYouTubeUrl(videoId);
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = _fixture.CreateGuid(),
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = youTubeUrl }
            },
            YouTubeId = null
        };
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(report));
        FreshNewsCopy(report, "etag-1");
        CataloguePlayableFieldPatch? patch = null;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.PatchFields(report.NewsOrganisationId, report.Id, "etag-1",
                It.IsAny<CataloguePlayableFieldPatch>()))
            .Callback((Guid _, Guid _, string _, CataloguePlayableFieldPatch p) => patch = p)
            .ReturnsAsync(CataloguePlayablePatchOutcome.Patched);
        IReadOnlyList<EpisodeSearchRecord>? uploaded = null;
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) => uploaded = docs)
            .ReturnsAsync(true);
        var changeLog = Path.Combine(_tempDir, "youtube-id-only.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            YouTubeIdOnly = true,
            Kind = "newsreport",
            Apply = true,
            Consent = CataloguePlayableServicesBackfillConsent.ApplyPhrase,
            ChangeLog = changeLog
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.Applied.Should().Be(1);
        result.SearchUploaded.Should().Be(1);
        report.YouTubeId.Should().Be(videoId);
        report.Services![ServiceKeys.YouTube].Url.Should().Be(youTubeUrl);
        uploaded.Should().ContainSingle().Which.YoutubeId.Should().Be(videoId);
        patch.Should().Be(new CataloguePlayableFieldPatch { SetYouTubeId = true, YouTubeId = videoId });
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when --youtube-id-only runs and an episode backup holds a different " +
        "service url, then services are left alone and only an already-synced item is identical, because the mode " +
        "never carries truth-source urls.")]
    public async Task youtube_id_only_ignores_truth_sources()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var videoId = _fixture.CreateYouTubeId();
        var film = new Film(_fixture.CreateTitle())
        {
            Id = id,
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            },
            YouTubeId = videoId
        };
        _backups.ById[id] = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAll())
            .Returns(ToAsync(film));
        var changeLog = Path.Combine(_tempDir, "youtube-id-only-identical.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            YouTubeIdOnly = true,
            EpisodeBackupDir = _tempDir,
            Kind = "film",
            ChangeLog = changeLog
        });

        // Assert
        result.Identical.Should().Be(1);
        result.Discrepancies.Should().Be(0);
        film.Services.Should().ContainSingle().Which.Key.Should().Be(ServiceKeys.YouTube);
        (await File.ReadAllLinesAsync(changeLog)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a news report is edited elsewhere between the scan and the write " +
        "(new title), then apply patches only /youTubeId against the fresh ETag, so the newer title survives and is " +
        "what the search document carries.")]
    public async Task concurrent_unrelated_edit_survives_partial_patch()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var scanned = UrlOnlyReport(videoId);
        var fresh = UrlOnlyReport(videoId, scanned.Id, scanned.NewsOrganisationId);
        fresh.Title = _fixture.CreateTitle();
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsync(scanned));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(scanned.NewsOrganisationId, scanned.Id))
            .ReturnsAsync(new ETagged<NewsReport>(fresh, "etag-fresh"));
        CataloguePlayableFieldPatch? patch = null;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.PatchFields(scanned.NewsOrganisationId, scanned.Id, "etag-fresh",
                It.IsAny<CataloguePlayableFieldPatch>()))
            .Callback((Guid _, Guid _, string _, CataloguePlayableFieldPatch p) => patch = p)
            .ReturnsAsync(CataloguePlayablePatchOutcome.Patched);
        IReadOnlyList<EpisodeSearchRecord>? uploaded = null;
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) => uploaded = docs)
            .ReturnsAsync(true);
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply("concurrent-unrelated.jsonl"));

        // Assert
        result.Applied.Should().Be(1);
        result.SkippedConflict.Should().Be(0);
        patch.Should().Be(new CataloguePlayableFieldPatch { SetYouTubeId = true, YouTubeId = videoId });
        uploaded.Should().ContainSingle().Which.Title.Should().Be(fresh.Title);
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when the YouTube url of a news report changes between the scan and " +
        "the write, then apply skips it as a conflict and patches nothing, because the planned id came from the old url.")]
    public async Task concurrent_youtube_url_edit_is_a_conflict()
    {
        // Arrange
        var scanned = UrlOnlyReport(_fixture.CreateYouTubeId());
        var fresh = UrlOnlyReport(_fixture.CreateYouTubeId(), scanned.Id, scanned.NewsOrganisationId);
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsync(scanned));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(scanned.NewsOrganisationId, scanned.Id))
            .ReturnsAsync(new ETagged<NewsReport>(fresh, "etag-fresh"));
        var changeLog = Path.Combine(_tempDir, "concurrent-url.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply(changeLog));

        // Assert
        result.ExitCode.Should().Be(0);
        result.Applied.Should().Be(0);
        result.SkippedConflict.Should().Be(1);
        result.FailedWrites.Should().Be(0);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.PatchFields(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CataloguePlayableFieldPatch>()), Times.Never);
        var record = ReadSingle(changeLog);
        record.Applied.Should().BeFalse();
        record.Skipped.Should().Be(CataloguePlayableServicesBackfillProcessor.SkippedConflict);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when Cosmos rejects the patch because the ETag moved on (412), then " +
        "the TV episode is skipped as a conflict, nothing is pushed to search and the run still exits 0.")]
    public async Task etag_mismatch_on_patch_is_a_conflict()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = UrlOnlyTvEpisode(videoId);
        _mocker.GetMock<ITvShowEpisodeRepository>().Setup(r => r.GetAll()).Returns(ToAsync(episode));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetTvShowEpisodeWithETag(episode.TvShowId, episode.Id))
            .ReturnsAsync(new ETagged<RedditPodcastPoster.Models.TvShows.TvShowEpisode>(
                UrlOnlyTvEpisode(videoId, episode.Id, episode.TvShowId), "etag-1"));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.PatchFields(episode.TvShowId, episode.Id, "etag-1", It.IsAny<CataloguePlayableFieldPatch>()))
            .ReturnsAsync(CataloguePlayablePatchOutcome.Conflict);
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply("etag-mismatch.jsonl", "tvshowepisode"));

        // Assert
        result.ExitCode.Should().Be(0);
        result.SkippedConflict.Should().Be(1);
        result.Applied.Should().Be(0);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Verify(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Verify(r => r.Save(It.IsAny<RedditPodcastPoster.Models.TvShows.TvShowEpisode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a news report is deleted or moved after the scan, then apply " +
        "skips it as not-found and never writes it back, because a partial patch must not resurrect a document.")]
    public async Task deleted_mid_run_is_not_recreated()
    {
        // Arrange
        var report = UrlOnlyReport(_fixture.CreateYouTubeId());
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsync(report));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(report.NewsOrganisationId, report.Id))
            .ReturnsAsync((ETagged<NewsReport>?)null);
        var changeLog = Path.Combine(_tempDir, "deleted.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply(changeLog));

        // Assert
        result.ExitCode.Should().Be(0);
        result.SkippedNotFound.Should().Be(1);
        result.Applied.Should().Be(0);
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.PatchFields(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CataloguePlayableFieldPatch>()), Times.Never);
        ReadSingle(changeLog).Skipped.Should().Be(CataloguePlayableServicesBackfillProcessor.SkippedNotFound);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a TV episode is deleted between the ETag read and the patch, " +
        "then Cosmos answers 404 and the row is skipped as not-found, not counted as a failed write.")]
    public async Task deleted_between_read_and_patch_is_skipped()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = UrlOnlyTvEpisode(videoId);
        _mocker.GetMock<ITvShowEpisodeRepository>().Setup(r => r.GetAll()).Returns(ToAsync(episode));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetTvShowEpisodeWithETag(episode.TvShowId, episode.Id))
            .ReturnsAsync(new ETagged<RedditPodcastPoster.Models.TvShows.TvShowEpisode>(
                UrlOnlyTvEpisode(videoId, episode.Id, episode.TvShowId), "etag-1"));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.PatchFields(episode.TvShowId, episode.Id, "etag-1", It.IsAny<CataloguePlayableFieldPatch>()))
            .ReturnsAsync(CataloguePlayablePatchOutcome.NotFound);
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply("deleted-404.jsonl", "tvshowepisode"));

        // Assert
        result.ExitCode.Should().Be(0);
        result.SkippedNotFound.Should().Be(1);
        result.FailedWrites.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: when a stored YouTubeId disagrees with services.youtube.url, then the " +
        "dry run counts it as overwritten and logs the real old and new ids, because url wins and the operator must " +
        "be able to review or roll back an overwrite.")]
    public async Task dry_run_logs_overwrite_with_real_ids()
    {
        // Arrange
        var urlId = _fixture.CreateYouTubeId();
        var staleId = _fixture.CreateYouTubeId();
        var report = UrlOnlyReport(urlId);
        report.YouTubeId = staleId;
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsync(report));
        var changeLog = Path.Combine(_tempDir, "overwrite.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(new CataloguePlayableServicesBackfillRequest
        {
            YouTubeIdOnly = true,
            Kind = "newsreport",
            ChangeLog = changeLog
        });

        // Assert
        result.Discrepancies.Should().Be(1);
        result.YouTubeIdFilled.Should().Be(0);
        result.YouTubeIdOverwritten.Should().Be(1);
        result.YouTubeIdCleared.Should().Be(0);
        var record = ReadSingle(changeLog);
        record.YouTubeIdOutcome.Should().Be(CataloguePlayableServicesBackfillProcessor.YouTubeIdOverwritten);
        record.Changes.Should().ContainSingle().Which.Should()
            .Be(new ServiceFieldChange(ServiceKeys.YouTube, "youTubeId", staleId, urlId));
    }

    [Theory(DisplayName =
        "Catalogue playable services backfill: when services.youtube.url is missing or not a video url but a YouTubeId " +
        "is stored, then the id is kept, nothing is written, and the row is logged as skipped-unparseable, so the " +
        "backfill never loses an id.")]
    [InlineData(null)]
    [InlineData("https://www.youtube.com/@somechannel")]
    public async Task unparseable_url_keeps_existing_id(string? url)
    {
        // Arrange
        var existingId = _fixture.CreateYouTubeId();
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = _fixture.CreateGuid(),
            Services = url is null
                ? null
                : new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = new() { Url = new Uri(url) } },
            YouTubeId = existingId
        };
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsync(report));
        var changeLog = Path.Combine(_tempDir, "unparseable.jsonl");
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply(changeLog));

        // Assert
        result.Identical.Should().Be(1);
        result.Discrepancies.Should().Be(0);
        result.YouTubeIdSkippedUnparseable.Should().Be(1);
        result.YouTubeIdCleared.Should().Be(0);
        report.YouTubeId.Should().Be(existingId);
        var record = ReadSingle(changeLog);
        record.YouTubeIdOutcome.Should().Be(CataloguePlayableServicesBackfillProcessor.YouTubeIdSkippedUnparseable);
        record.Changes.Should().BeEmpty();
        record.Applied.Should().BeFalse();
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.GetNewsReportWithETag(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue playable services backfill: applied search documents are uploaded in batches, not one call per " +
        "document, so a run over thousands of reports is not thousands of sequential search round trips.")]
    public async Task search_documents_are_batched()
    {
        // Arrange
        var reports = Enumerable.Range(0, 3).Select(_ => UrlOnlyReport(_fixture.CreateYouTubeId())).ToArray();
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll()).Returns(ToAsyncMany(reports));
        foreach (var report in reports)
        {
            FreshNewsCopy(report, "etag-" + report.Id);
        }

        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.PatchFields(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<CataloguePlayableFieldPatch>()))
            .ReturnsAsync(CataloguePlayablePatchOutcome.Patched);
        var calls = new List<int>();
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<EpisodeSearchRecord> docs, CancellationToken _) => calls.Add(docs.Count))
            .ReturnsAsync(true);
        var sut = _mocker.CreateInstance<CataloguePlayableServicesBackfillProcessor>();

        // Act
        var result = await sut.Run(YouTubeIdOnlyApply("batched.jsonl"));

        // Assert
        result.Applied.Should().Be(3);
        result.SearchUploaded.Should().Be(3);
        result.YouTubeIdFilled.Should().Be(3);
        calls.Should().Equal(3);
    }

    private NewsReport UrlOnlyReport(string videoId, Guid? id = null, Guid? organisationId = null) =>
        new(_fixture.CreateTitle())
        {
            Id = id ?? _fixture.CreateGuid(),
            NewsOrganisationId = organisationId ?? _fixture.CreateGuid(),
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            },
            YouTubeId = null
        };

    private RedditPodcastPoster.Models.TvShows.TvShowEpisode UrlOnlyTvEpisode(
        string videoId, Guid? id = null, Guid? tvShowId = null) =>
        new(_fixture.CreateTitle())
        {
            Id = id ?? _fixture.CreateGuid(),
            TvShowId = tvShowId ?? _fixture.CreateGuid(),
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(videoId) }
            },
            YouTubeId = null
        };

    /// <summary>The ETag re-read returns an unchanged copy of the scanned report.</summary>
    private void FreshNewsCopy(NewsReport scanned, string eTag)
    {
        var copy = new NewsReport(scanned.Title)
        {
            Id = scanned.Id,
            NewsOrganisationId = scanned.NewsOrganisationId,
            Services = scanned.Services?.ToDictionary(
                x => x.Key, x => new ServiceLink { Url = x.Value.Url, Image = x.Value.Image }),
            YouTubeId = scanned.YouTubeId
        };
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetNewsReportWithETag(scanned.NewsOrganisationId, scanned.Id))
            .ReturnsAsync(new ETagged<NewsReport>(copy, eTag));
    }

    private CataloguePlayableServicesBackfillRequest YouTubeIdOnlyApply(
        string changeLog, string kind = "newsreport") => new()
    {
        YouTubeIdOnly = true,
        Kind = kind,
        Apply = true,
        Consent = CataloguePlayableServicesBackfillConsent.ApplyPhrase,
        ChangeLog = Path.IsPathRooted(changeLog) ? changeLog : Path.Combine(_tempDir, changeLog)
    };

    private static ChangeLogRecord ReadSingle(string changeLog) =>
        JsonSerializer.Deserialize<ChangeLogRecord>(
            File.ReadAllLines(changeLog).Single(),
            BackupDocumentJsonOptions.Instance)!;

    private static async IAsyncEnumerable<T> ToAsyncMany<T>(
        T[] items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var item in items)
        {
            yield return item;
        }
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
