using System.Net;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Thumbnails;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;
using RedditPodcastPoster.PodcastServices.Thumbnails;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using static RedditPodcastPoster.Episodes.TestSupport.Fixtures.YouTubeThumbnailSpecimens;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Thumbnails;

/// <summary>
/// Round-robin over the eligible set. Real case (#1028, 2026-10-14 13:00 BST run): 77 episodes were eligible,
/// the cap probed 25 and 52 were deferred, and every run restarted from the newest, so anything outside the
/// newest 25 was never checked. Eligible episodes are now ordered by release time then id; each run starts
/// strictly after the persisted cursor and wraps at the end, so every eligible episode gets an equal turn.
/// </summary>
public class RecentYouTubeThumbnailRecheckRoundRobinRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly YouTubeThumbnailHttpStub _http = new();
    private readonly YouTubeThumbnailRecheckOptions _options = new();
    private readonly InMemoryCursorStore _cursorStore = new();
    private readonly FakeTimeProvider _time;
    private readonly DateTime _now;
    private readonly Podcast _podcast;
    private readonly List<Google.Apis.YouTube.v3.Data.Video> _videos = [];
    private readonly Dictionary<Guid, Episode> _stored = new();

    public RecentYouTubeThumbnailRecheckRoundRobinRules()
    {
        _now = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(13));
        _time = new FakeTimeProvider(new DateTimeOffset(_now, TimeSpan.Zero));
        _podcast = _fixture.CreatePodcast();
        _mocker.Use<TimeProvider>(_time);
        _mocker.Use(Options.Create(_options));
        _mocker.Use<IYouTubeThumbnailRecheckCursorStore>(_cursorStore);
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(YouTubeThumbnailResolver.HttpClientName))
            .Returns(() => new HttpClient(_http, disposeHandler: false));
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IYouTubeServiceWrapper _, IEnumerable<string> ids, IndexingContext _, bool _, bool _,
                bool _) => _videos.Where(video => ids.Contains(video.Id)).ToList());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(repository => repository.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid _, Guid episodeId) => _stored.GetValueOrDefault(episodeId));
        _mocker.Use<IYouTubeThumbnailResolver>(_mocker.CreateInstance<YouTubeThumbnailResolver>());
        _mocker.Use<IImageUpdater>(_mocker.CreateInstance<ImageUpdater>());
    }

    [Fact(DisplayName =
        "Real case: 77 eligible episodes with a cap of 25 probes 25 and defers 52 on the first run, and every " +
        "episode has been probed within 4 runs, because each run resumes after the last one and wraps.")]
    public async Task seventy_seven_eligible_are_all_covered_within_four_runs()
    {
        // Arrange
        var episodes = LiveEpisodes(77);

        // Act
        var first = await Run(episodes);
        var afterOne = ProbedEpisodes(episodes).Count;
        await Run(episodes);
        await Run(episodes);
        var afterThree = ProbedEpisodes(episodes).Count;
        await Run(episodes);

        // Assert
        first.Eligible.Should().Be(77);
        first.Probed.Should().Be(25);
        first.DeferredOverBudget.Should().Be(52);
        afterOne.Should().Be(25);
        afterThree.Should().Be(75);
        ProbedEpisodes(episodes).Should().HaveCount(77);
        _cursorStore.Saves.Should().Be(4);
    }

    [Fact(DisplayName =
        "With no cursor stored, the first run starts from the oldest eligible release, so episodes about to leave " +
        "the window are checked before they age out.")]
    public async Task no_cursor_starts_at_the_oldest()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 2;
        var episodes = LiveEpisodes(4);

        // Act
        await Run(episodes);

        // Assert
        _http.Requested.Should().Equal(StoredImage(episodes[0]), StoredImage(episodes[1]));
        _cursorStore.Cursor!.LastProbedEpisodeId.Should().Be(episodes[1].Id);
        _cursorStore.Cursor.LastProbedReleaseUtc.Should().Be(episodes[1].ReleaseUtc);
    }

    [Fact(DisplayName =
        "A run starts strictly after the stored cursor, and episodes with the same release time are ordered by id.")]
    public async Task resumes_strictly_after_cursor_with_id_tie_break()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 2;
        var release = _now.AddHours(-5);
        var tied = Enumerable.Range(0, 3).Select(_ => LiveEpisode(release)).OrderBy(e => e.Id).ToList();
        _cursorStore.Cursor = new YouTubeThumbnailRecheckCursor
        {
            LastProbedReleaseUtc = release,
            LastProbedEpisodeId = tied[0].Id
        };

        // Act
        await Run(tied);

        // Assert
        _http.Requested.Should().Equal(StoredImage(tied[1]), StoredImage(tied[2]));
    }

    [Fact(DisplayName =
        "When the cursor episode has left the window, the run starts at the first eligible episode after the " +
        "cursor's release time, which is the oldest still in the window.")]
    public async Task cursor_episode_left_window()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 1;
        var episodes = LiveEpisodes(3);
        _cursorStore.Cursor = new YouTubeThumbnailRecheckCursor
        {
            LastProbedReleaseUtc = _now.AddHours(-(_options.RecheckWindowHours + 3)),
            LastProbedEpisodeId = Guid.NewGuid()
        };

        // Act
        await Run(episodes);

        // Assert
        _http.Requested.Should().Equal(StoredImage(episodes[0]));
    }

    [Fact(DisplayName = "With nothing eligible, the run probes nothing and does not write the cursor.")]
    public async Task empty_set_writes_nothing()
    {
        // Arrange

        // Act
        var result = await Run([]);

        // Assert
        result.Eligible.Should().Be(0);
        _cursorStore.Saves.Should().Be(0);
    }

    [Fact(DisplayName =
        "With no more eligible episodes than the cap, every one is probed each run and the next run wraps " +
        "back to the oldest.")]
    public async Task fewer_than_cap_wraps_each_run()
    {
        // Arrange
        var episodes = LiveEpisodes(3);

        // Act
        var first = await Run(episodes);
        var firstRequests = _http.Requested.ToList();
        await Run(episodes);

        // Assert
        first.Probed.Should().Be(3);
        first.DeferredOverBudget.Should().Be(0);
        firstRequests.Should().Equal(episodes.Select(e => StoredImage(e)!));
        _http.Requested.Skip(3).Should().Equal(episodes.Select(e => StoredImage(e)!));
    }

    [Fact(DisplayName =
        "An episode released after the cursor was written is reached in release order on the next run, " +
        "before the run wraps.")]
    public async Task new_arrival_is_reached_before_wrap()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 2;
        var episodes = LiveEpisodes(3);
        await Run(episodes);
        var arrival = LiveEpisode(_now.AddMinutes(-10));
        var withArrival = episodes.Append(arrival).ToList();
        var before = _http.Requested.Count;

        // Act
        await Run(withArrival);

        // Assert
        _http.Requested.Skip(before).Should().Equal(StoredImage(episodes[2]), StoredImage(arrival));
    }

    [Fact(DisplayName =
        "A stored-image probe that does not answer within the per-probe timeout counts as failed, is not treated " +
        "as withdrawn (no re-resolve), and the run carries on.")]
    public async Task probe_timeout_counts_failed_not_withdrawn()
    {
        // Arrange
        var episodes = LiveEpisodes(2);
        var hanging = StoredImage(episodes[0])!;
        _http.Hang(hanging);
        _http.OnRequest = url =>
        {
            if (url == hanging)
            {
                _time.Advance(TimeSpan.FromSeconds(_options.ProbeTimeoutSeconds + 1));
            }
        };

        // Act
        var result = await Run(episodes);

        // Assert
        result.Failed.Should().Be(1);
        result.StillUsable.Should().Be(1);
        result.Replaced.Should().Be(0);
        _mocker.GetMock<IYouTubeVideoService>().Verify(service => service.GetVideoContentDetails(
            It.IsAny<IYouTubeServiceWrapper>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexingContext>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the run budget is spent, the remaining episodes are deferred and the cursor stays at the last " +
        "probed episode, so the next run picks them up.")]
    public async Task run_budget_defers_the_rest()
    {
        // Arrange
        var episodes = LiveEpisodes(25);
        _http.OnRequest = _ => _time.Advance(TimeSpan.FromSeconds(3));

        // Act
        var result = await Run(episodes);

        // Assert
        result.Probed.Should().Be(20);
        result.DeferredOverBudget.Should().Be(5);
        _cursorStore.Cursor!.LastProbedEpisodeId.Should().Be(episodes[19].Id);
    }

    [Fact(DisplayName = "A cursor at or after the newest eligible episode wraps the next run to the oldest.")]
    public void cursor_at_newest_wraps_to_start()
    {
        // Arrange
        var episodes = LiveEpisodes(3);
        var cursor = new YouTubeThumbnailRecheckCursor
        {
            LastProbedReleaseUtc = episodes[2].ReleaseUtc,
            LastProbedEpisodeId = episodes[2].Id
        };

        // Act
        var start = RecentYouTubeThumbnailRechecker.RoundRobinStartIndex(episodes, cursor); // pragma: allowlist secret

        // Assert
        start.Should().Be(0);
    }

    [Fact(DisplayName = "A cursor with a release time but no episode id resumes at the first episode with that release time.")]
    public void cursor_with_null_episode_id_positions_by_release()
    {
        // Arrange
        var episodes = LiveEpisodes(3);
        var cursor = new YouTubeThumbnailRecheckCursor { LastProbedReleaseUtc = episodes[1].ReleaseUtc };

        // Act
        var start = RecentYouTubeThumbnailRechecker.RoundRobinStartIndex(episodes, cursor); // pragma: allowlist secret

        // Assert
        start.Should().Be(1);
    }

    [Fact(DisplayName =
        "When saving the round-robin cursor throws, the run still reports the probes it finished, because a lost " +
        "cursor only makes the next run start from the oldest.")]
    public async Task cursor_save_failure_does_not_fail_the_run()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 1;
        _cursorStore.ThrowOnSave = true;
        var episodes = LiveEpisodes(2); // pragma: allowlist secret

        // Act
        var result = await Run(episodes); // pragma: allowlist secret

        // Assert
        result.Probed.Should().Be(1);
        result.Failed.Should().Be(0);
        result.CursorEpisodeId.Should().Be(episodes[0].Id); // pragma: allowlist secret
        _cursorStore.Cursor.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the probe budget is already spent, nothing is probed and there is no cursor to report, because the run deferred every eligible episode.")]
    public async Task zero_probe_budget_leaves_no_cursor()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 0;
        var episodes = LiveEpisodes(2); // pragma: allowlist secret

        // Act
        var result = await Run(episodes); // pragma: allowlist secret

        // Assert
        result.Probed.Should().Be(0);
        result.DeferredOverBudget.Should().Be(2);
        result.CursorEpisodeId.Should().BeNull();
        result.CursorReleaseUtc.Should().BeNull();
        _cursorStore.Saves.Should().Be(0);
    }

    [Fact(DisplayName =
        "When the probe budget is already spent and a cursor already exists, the result keeps that cursor, because this run did not probe.")]
    public async Task zero_probe_budget_keeps_the_previous_cursor()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 0;
        var episodes = LiveEpisodes(2); // pragma: allowlist secret
        var previous = new YouTubeThumbnailRecheckCursor
        {
            LastProbedEpisodeId = episodes[0].Id, // pragma: allowlist secret
            LastProbedReleaseUtc = episodes[0].ReleaseUtc // pragma: allowlist secret
        };
        _cursorStore.Cursor = previous;

        // Act
        var result = await Run(episodes); // pragma: allowlist secret

        // Assert
        result.Probed.Should().Be(0);
        result.CursorEpisodeId.Should().Be(previous.LastProbedEpisodeId);
        result.CursorReleaseUtc.Should().Be(previous.LastProbedReleaseUtc);
        _cursorStore.Saves.Should().Be(0);
    }

    [Fact(DisplayName = "No cursor, or a cursor with null fields, starts at the oldest.")]
    public void null_cursor_starts_at_zero()
    {
        // Arrange
        var episodes = LiveEpisodes(2);

        // Act
        var fromNull = RecentYouTubeThumbnailRechecker.RoundRobinStartIndex(episodes, null); // pragma: allowlist secret
        var fromEmpty = RecentYouTubeThumbnailRechecker.RoundRobinStartIndex(episodes, new YouTubeThumbnailRecheckCursor()); // pragma: allowlist secret

        // Assert
        fromNull.Should().Be(0);
        fromEmpty.Should().Be(0);
    }

    private async Task<YouTubeThumbnailRecheckResult> Run(IReadOnlyList<Episode> episodes) =>
        await _mocker.CreateInstance<RecentYouTubeThumbnailRechecker>().Recheck(
            episodes.Select(e => new PodcastEpisode(_podcast, e)).Reverse().ToList(),
            new IndexingContext());

    /// <summary>Eligible live maxres episodes, oldest first, spread inside the window.</summary>
    private List<Episode> LiveEpisodes(int count)
    {
        var span = TimeSpan.FromHours(_options.RecheckWindowHours - 1);
        return Enumerable.Range(0, count)
            .Select(i => LiveEpisode(_now - span + TimeSpan.FromTicks(span.Ticks / count * i)))
            .ToList();
    }

    private Episode LiveEpisode(DateTime release)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast, release);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        var image = Url(youTubeId, MaxresTier);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube,
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube), image);
        _http.Serve(image, HttpStatusCode.OK, MaxresJpeg());
        _stored[episode.Id] = episode;
        return episode;
    }

    private HashSet<Guid> ProbedEpisodes(IEnumerable<Episode> episodes)
    {
        var requested = _http.Requested.ToHashSet();
        return episodes.Where(e => requested.Contains(StoredImage(e)!)).Select(e => e.Id).ToHashSet();
    }

    private static Uri? StoredImage(Episode episode) => EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube);

    private sealed class InMemoryCursorStore : IYouTubeThumbnailRecheckCursorStore
    {
        public YouTubeThumbnailRecheckCursor? Cursor { get; set; }
        public int Saves { get; private set; }
        public bool ThrowOnSave { get; set; }

        public Task<YouTubeThumbnailRecheckCursor?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Cursor);

        public Task SaveAsync(YouTubeThumbnailRecheckCursor cursor, CancellationToken cancellationToken = default) =>
            SaveCore(cursor);

        private Task SaveCore(YouTubeThumbnailRecheckCursor cursor)
        {
            Saves++;
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("cursor store unavailable");
            }

            Cursor = cursor;
            return Task.CompletedTask;
        }
    }
}
