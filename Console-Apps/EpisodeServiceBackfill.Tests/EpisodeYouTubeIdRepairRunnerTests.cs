using EpisodeServiceBackfill;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace EpisodeServiceBackfill.Tests;

public class EpisodeYouTubeIdRepairRunnerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IEpisodeYouTubeIdRepairStore> _store;
    private readonly List<EpisodeYouTubeIdRepairPlan> _patched = [];
    private List<string> _candidates = [];
    private List<string> _storedIdDocuments = [];
    private IReadOnlyList<Guid>? _candidateIdsQueried;
    private Func<EpisodeYouTubeIdRepairPlan, YouTubeIdPatchOutcome> _patchOutcome = _ => YouTubeIdPatchOutcome.Patched;

    public EpisodeYouTubeIdRepairRunnerTests()
    {
        _mocker.Use(NullLogger<EpisodeYouTubeIdRepairRunner>.Instance);
        _store = _mocker.GetMock<IEpisodeYouTubeIdRepairStore>();
        _store.Setup(x => x.QueryCandidates(It.IsAny<IReadOnlyList<Guid>>()))
            .Returns((IReadOnlyList<Guid> ids) =>
            {
                _candidateIdsQueried = ids;
                return Stream(_candidates);
            });
        _store.Setup(x => x.QueryStoredIdsWithUrl(It.IsAny<IReadOnlyList<Guid>>()))
            .Returns(() => Stream(_storedIdDocuments));
        _store.Setup(x => x.Patch(It.IsAny<EpisodeYouTubeIdRepairPlan>()))
            .ReturnsAsync((EpisodeYouTubeIdRepairPlan plan) =>
            {
                _patched.Add(plan);
                return _patchOutcome(plan);
            });
    }

    [Fact(DisplayName =
        "youtube-id-only dry run: when candidates exist, then they are planned and listed but nothing is patched, because --apply was not given.")]
    public async Task dry_run_never_patches()
    {
        // Arrange
        _candidates = [MissingIdJson(out _), MissingIdJson(out _)];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: false));

        // Assert
        result.ExitCode.Should().Be(0);
        result.Plans.Should().HaveCount(2);
        _patched.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "youtube-id-only apply: when every patch succeeds, then each planned episode is patched once and the run exits 0.")]
    public async Task apply_patches_each_plan()
    {
        // Arrange
        _candidates = [MissingIdJson(out var first), MissingIdJson(out var second)];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: true));

        // Assert
        result.ExitCode.Should().Be(0);
        _patched.Select(x => x.EpisodeId).Should().BeEquivalentTo([first, second]);
        result.Outcomes[YouTubeIdPatchOutcome.Patched].Should().Be(2);
    }

    [Theory(DisplayName =
        "youtube-id-only apply: a document changed or deleted since the scan is counted and skipped without failing the run, because nothing was written.")]
    [InlineData(YouTubeIdPatchOutcome.ConditionNotMet)]
    [InlineData(YouTubeIdPatchOutcome.NotFound)]
    public async Task changed_or_missing_documents_do_not_fail_the_run(YouTubeIdPatchOutcome outcome)
    {
        // Arrange
        _candidates = [MissingIdJson(out _)];
        _patchOutcome = _ => outcome;
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: true));

        // Assert
        result.ExitCode.Should().Be(0);
        result.Outcomes[outcome].Should().Be(1);
    }

    [Theory(DisplayName =
        "youtube-id-only apply: when a patch fails or is rejected, then the remaining plans are still attempted and the run exits 2, so the operator re-checks.")]
    [InlineData(YouTubeIdPatchOutcome.Failed)]
    [InlineData(YouTubeIdPatchOutcome.BadRequest)]
    public async Task failed_patch_exits_two(YouTubeIdPatchOutcome outcome)
    {
        // Arrange
        _candidates = [MissingIdJson(out var failing), MissingIdJson(out _)];
        _patchOutcome = plan => plan.EpisodeId == failing ? outcome : YouTubeIdPatchOutcome.Patched;
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: true));

        // Assert
        result.ExitCode.Should().Be(EpisodeYouTubeIdRepairRunner.PatchFailuresExitCode);
        _patched.Should().HaveCount(2);
        result.Outcomes[outcome].Should().Be(1);
    }

    [Fact(DisplayName =
        "youtube-id-only: without --ids or --all, --limit keeps the oldest candidates by _ts, so a first apply touches only a few documents.")]
    public async Task limit_keeps_oldest()
    {
        // Arrange
        _candidates = [MissingIdJson(out var newest, ts: 300), MissingIdJson(out var oldest, ts: 100), MissingIdJson(out var middle, ts: 200)];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: false, limit: 2));

        // Assert
        result.Plans.Select(x => x.EpisodeId).Should().Equal(oldest, middle);
        result.Plans.Should().NotContain(x => x.EpisodeId == newest);
    }

    [Fact(DisplayName = "youtube-id-only: --all ignores --limit and plans every candidate.")]
    public async Task all_ignores_limit()
    {
        // Arrange
        _candidates = [MissingIdJson(out _), MissingIdJson(out _), MissingIdJson(out _)];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: false, limit: 1, all: true));

        // Assert
        result.Plans.Should().HaveCount(3);
    }

    [Fact(DisplayName =
        "youtube-id-only: --ids passes the parsed GUIDs to the query and ignores --limit, so the named episodes are all planned.")]
    public async Task ids_select_named_episodes()
    {
        // Arrange
        _candidates = [MissingIdJson(out var first), MissingIdJson(out var second)];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: false, limit: 1, ids: $"{first}, {second};{first}"));

        // Assert
        _candidateIdsQueried.Should().Equal(first, second);
        result.Plans.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "youtube-id-only: when --ids contains something that is not a GUID, then the run exits 1 before querying, because a typo must not widen the run.")]
    public async Task invalid_ids_exit_before_querying()
    {
        // Arrange
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: true, ids: $"{_fixture.CreateGuid()},not-a-guid"));

        // Assert
        result.ExitCode.Should().Be(EpisodeYouTubeIdRepairRunner.InvalidArgumentsExitCode);
        _candidateIdsQueried.Should().BeNull();
        _patched.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "youtube-id-only dry run: stored ids that disagree with the url are listed as mismatches and never patched, because podcast episodes keep their stored id.")]
    public async Task dry_run_reports_mismatches_without_patching()
    {
        // Arrange
        var (episodeId, podcastId) = (_fixture.CreateGuid(), _fixture.CreateGuid());
        var (storedId, urlId) = (_fixture.CreateYouTubeId(), _fixture.CreateYouTubeId());
        _storedIdDocuments =
        [
            $$"""{"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"youtube":"{{storedId}}"},"services":{"youtube":{"url":"https://youtu.be/{{urlId}}"} } }""",
            StoredIdJson()
        ];
        var sut = _mocker.CreateInstance<EpisodeYouTubeIdRepairRunner>();

        // Act
        var result = await sut.Execute(Request(apply: false));

        // Assert
        result.Mismatches.Should().ContainSingle()
            .Which.Should().Be(new EpisodeYouTubeIdMismatch(episodeId, podcastId, storedId, urlId, $"https://youtu.be/{urlId}"));
        _patched.Should().BeEmpty();
    }

    private static EpisodeServiceBackfillRequest Request(bool apply, int limit = 4, bool all = false, string? ids = null) =>
        new() { YouTubeIdOnly = true, Apply = apply, Limit = limit, All = all, Ids = ids };

    private string MissingIdJson(out Guid episodeId, long ts = 1790000000)
    {
        episodeId = _fixture.CreateGuid();
        return $$"""
            {"id":"{{episodeId}}","podcastId":"{{_fixture.CreateGuid()}}","_ts":{{ts}},"ids":{},
             "services":{"youtube":{"url":"{{_fixture.DefaultYouTubeUrl(_fixture.CreateYouTubeId())}}"} } }
            """;
    }

    private string StoredIdJson()
    {
        var videoId = _fixture.CreateYouTubeId();
        return $$"""
            {"id":"{{_fixture.CreateGuid()}}","podcastId":"{{_fixture.CreateGuid()}}","ids":{"youtube":"{{videoId}}"},
             "services":{"youtube":{"url":"{{_fixture.DefaultYouTubeUrl(videoId)}}"} } }
            """;
    }

    private static async IAsyncEnumerable<string> Stream(IEnumerable<string> documents)
    {
        foreach (var document in documents)
        {
            yield return document;
        }

        await Task.CompletedTask;
    }
}
