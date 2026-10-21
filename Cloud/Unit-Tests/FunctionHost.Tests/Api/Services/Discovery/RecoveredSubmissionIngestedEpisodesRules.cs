using System.Linq.Expressions;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

/// <summary>
/// Business rules for the /discovery-review "Ingested episodes" section of a submission recovered by hand, from the real
/// case: submission 4a7d8838 ingested all 11 results, its final write was cancelled, and an admin set it Applied via
/// Change state, which records no item outcomes. The detail must still list the episodes that exist for its accepted
/// results (looked up read-only by the results' platform ids), in the item-outcome shape the website already reads.
/// </summary>
public class RecoveredSubmissionIngestedEpisodesRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Episode> _episodes = [];
    private DiscoverySubmission _submission = null!;
    private DiscoveryResultsDocument _report = null!;

    public RecoveredSubmissionIngestedEpisodesRules()
    {
        _mocker.Use(TimeProvider.System);
        _mocker.Use<IDiscoverySubmissionIngestedEpisodes>(
            _mocker.CreateInstance<DiscoverySubmissionIngestedEpisodes>());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns((Expression<Func<Episode, bool>> selector) =>
                _episodes.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IDiscoverySubmissionsRepository>()
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _submission);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetDocuments(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(() => [_report]);
        _mocker.GetMock<IDiscoveryResultsService>()
            .Setup(s => s.GetPodcastInfo(It.IsAny<IEnumerable<DiscoveryResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DiscoveryPodcastInfo>());
    }

    [Fact(DisplayName =
        "INVARIANT (submission 4a7d8838): An Applied submission with no item outcomes (recovered via Change state) lists as Succeeded each accepted result whose episode exists, matched by Spotify, Apple or YouTube id, so the Ingested episodes section shows them.")]
    public async Task recovered_applied_submission_lists_ingested_episodes()
    {
        // Arrange
        var (spotify, apple, youTube, missing, removed) = Arrange(DiscoverySubmissionStatus.Applied);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Get(_submission.Id, Curator(), CancellationToken.None);

        // Assert
        var outcomes = result.Value!.Submission.ItemOutcomes;
        outcomes.Should().NotBeNull();
        outcomes!.Should().OnlyContain(x => x.State == DiscoverySubmissionItemState.Succeeded);
        outcomes.Select(x => (x.ResultId, x.EpisodeId, x.PodcastId)).Should().BeEquivalentTo([
            (spotify.Result, spotify.Episode.Id, spotify.Episode.PodcastId),
            (apple.Result, apple.Episode.Id, apple.Episode.PodcastId),
            (youTube.Result, youTube.Episode.Id, youTube.Episode.PodcastId)
        ]);
        outcomes.Select(x => x.ResultId).Should().NotContain([missing, removed]);
    }

    [Fact(DisplayName =
        "INVARIANT: Resolving ingested episodes for a recovered submission is read-only: nothing is written to the submission or the reports.")]
    public async Task recovered_lookup_writes_nothing()
    {
        // Arrange
        Arrange(DiscoverySubmissionStatus.Applied);
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        await sut.Get(_submission.Id, Curator(), CancellationToken.None);

        // Assert
        _mocker.GetMock<IDiscoverySubmissionsRepository>().Verify(r => r.TryReplace(
            It.IsAny<DiscoverySubmission>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(It.IsAny<IEnumerable<Episode>>()), Times.Never);
        _mocker.GetMock<IDiscoveryResultsRepository>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "When the episode lookup for a recovered submission fails, the detail is still returned (without ingested episodes), because the lookup is a best-effort view and must not hide the submission.")]
    public async Task recovered_lookup_failure_still_returns_detail()
    {
        // Arrange
        Arrange(DiscoverySubmissionStatus.Applied);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Throws(new InvalidOperationException("cosmos unavailable"));
        _mocker.Use<IDiscoverySubmissionIngestedEpisodes>(_mocker.CreateInstance<DiscoverySubmissionIngestedEpisodes>());
        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Get(_submission.Id, Curator(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(DiscoverySubmissionQueryStatus.Ok);
        result.Value!.Submission.ItemOutcomes.Should().BeNull();
    }

    [Theory(DisplayName =
        "Recorded outcomes are the source of truth and a submission that is not Applied has no ingested set to infer, so no episode lookup is made for them.")]
    [InlineData(DiscoverySubmissionStatus.Pending, false)]
    [InlineData(DiscoverySubmissionStatus.Applying, false)]
    [InlineData(DiscoverySubmissionStatus.ApplyFailed, false)]
    [InlineData(DiscoverySubmissionStatus.Applied, true)]
    public async Task no_lookup_unless_applied_without_outcomes(DiscoverySubmissionStatus status, bool withOutcomes)
    {
        // Arrange
        Arrange(status);
        var recorded = new DiscoverySubmissionItemOutcome
        {
            ResultId = _fixture.Create<Guid>(), State = DiscoverySubmissionItemState.Succeeded,
            EpisodeId = _fixture.Create<Guid>()
        };
        if (withOutcomes)
        {
            _submission.ItemOutcomes = [recorded];
        }

        var sut = _mocker.CreateInstance<DiscoverySubmissionReviewService>();

        // Act
        var result = await sut.Get(_submission.Id, Curator(), CancellationToken.None);

        // Assert
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()),
            Times.Never);
        if (withOutcomes)
        {
            result.Value!.Submission.ItemOutcomes.Should().ContainSingle().Which.Should().BeSameAs(recorded);
        }
        else
        {
            result.Value!.Submission.ItemOutcomes.Should().BeNull();
        }
    }

    private ((Guid Result, Episode Episode) Spotify, (Guid Result, Episode Episode) Apple,
        (Guid Result, Episode Episode) YouTube, Guid Missing, Guid Removed) Arrange(DiscoverySubmissionStatus status)
    {
        var spotifyId = "4rOoJ6Egrf8K2IrywzwOMk";
        var appleId = 1000712345678L;
        var youTubeId = "dQw4w9WgXcQ";
        var spotify = Result(new DiscoveryResultUrls { Spotify = new Uri($"https://open.spotify.com/episode/{spotifyId}") });
        var apple = Result(new DiscoveryResultUrls
        {
            Apple = new Uri($"https://podcasts.apple.com/us/podcast/the-compendium/id1520000000?i={appleId}")
        });
        var youTube = Result(new DiscoveryResultUrls { YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}") });
        var missing = Result(new DiscoveryResultUrls { Spotify = new Uri("https://open.spotify.com/episode/0000000000000000000000") });
        var removed = Result(new DiscoveryResultUrls { Spotify = new Uri("https://open.spotify.com/episode/1111111111111111111111") });
        _report = new DiscoveryResultsDocument(DateTime.UtcNow.AddDays(-1), [spotify, apple, youTube, missing, removed])
        {
            SearchSince = _fixture.Create<string>(), State = DiscoveryResultsDocumentState.Processed
        };
        var spotifyEpisode = Episode(new EpisodeIds { Spotify = spotifyId });
        var appleEpisode = Episode(new EpisodeIds { Apple = appleId });
        var youTubeEpisode = Episode(new EpisodeIds { YouTube = youTubeId });
        // The supervisor removed this result, so its episode (ingested elsewhere) is not this submission's.
        Episode(new EpisodeIds { Spotify = "1111111111111111111111" });
        _submission = new DiscoverySubmission
        {
            Id = _fixture.Create<Guid>(),
            Status = status,
            SubmitterSubject = _fixture.Create<string>(),
            SubmittedAt = DateTime.UtcNow.AddHours(-1),
            ETag = _fixture.Create<string>(),
            DiscoveryResultsDocumentIds = [_report.Id],
            SubmitterAcceptedResultIds = [spotify.Id, apple.Id, youTube.Id, missing.Id, removed.Id],
            SupervisorAcceptedResultIds = [spotify.Id, apple.Id, youTube.Id, missing.Id],
            RemovedBySupervisorResultIds = [removed.Id]
        };
        return ((spotify.Id, spotifyEpisode), (apple.Id, appleEpisode), (youTube.Id, youTubeEpisode), missing.Id, removed.Id);
    }

    private DiscoveryResult Result(DiscoveryResultUrls urls) => new() { Id = _fixture.Create<Guid>(), Urls = urls };

    private Episode Episode(EpisodeIds ids)
    {
        var episode = new Episode { Id = _fixture.Create<Guid>(), PodcastId = _fixture.Create<Guid>(), Ids = ids };
        _episodes.Add(episode);
        return episode;
    }

    private SupervisionCaller Curator() => new(_submission.SubmitterSubject, IsAdmin: true, IsSupervisor: true);
}
