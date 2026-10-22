using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.IndexingHints;

/// <summary>
/// The playlist branch of YouTube discovery with indexing hints: a large playlist with fresh hints is read from the
/// channel's uploads head; any doubt falls back to the full walk, and only a complete full walk refreshes the hints.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerHintsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeRetrievalHandler> _logger = new();
    private readonly YouTubeIndexingHintsOptions _options = new();
    private readonly DateTime _now = DomainTestFixture.UtcDaysAgo(0).AddHours(12);
    private readonly IndexingContext _context = new(DomainTestFixture.UtcDaysAgo(2));
    private readonly Podcast _podcast;
    private GetPlaylistEpisodesResponse _walk = new([]);
    private YouTubeUploadsHeadResult _uploadsHead;
    private GetPlaylistEpisodesResponse _built = new([]);

    public YouTubeEpisodeRetrievalHandlerHintsRules()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _podcast.YouTubeIndexingHints = new YouTubeIndexingHints
        {
            PlaylistId = _podcast.YouTubePlaylistId,
            LastFullWalk = _now.AddHours(-3),
            PlaylistItemCount = _options.UploadsHeadMinimumPlaylistItems + 50
        };
        _uploadsHead = new YouTubeUploadsHeadResult([], _options.UploadsHeadMinimumPlaylistItems + 50, null, null, 0,
            null);
        _mocker.Use<ILogger<YouTubeEpisodeRetrievalHandler>>(_logger);
        _mocker.Use<IOptions<YouTubeIndexingHintsOptions>>(Options.Create(_options));
        _mocker.Use<TimeProvider>(new FixedTimeProvider(_now));
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(It.IsAny<Podcast>(), It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>(), It.IsAny<IEnumerable<string>?>()))
            .ReturnsAsync(() => _walk);
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.BuildPlaylistEpisodes(It.IsAny<Podcast>(), It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(), It.IsAny<IList<PlaylistItem>>(), It.IsAny<IEnumerable<string>?>()))
            .ReturnsAsync(() => _built);
        _mocker.GetMock<IYouTubeUploadsHeadCheck>()
            .Setup(x => x.Check(It.IsAny<IYouTubeIndexingSettings>(), It.IsAny<YouTubeIndexingHints>(),
                It.IsAny<YouTubePlaylistId>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(() => _uploadsHead);
    }

    private Task<EpisodeRetrievalHandlerResponse> Run(
        IndexingContext? context = null) =>
        _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>().GetEpisodes(_podcast, [], context ?? _context);

    private void VerifyFullWalk(Times times) =>
        _mocker.GetMock<IYouTubeEpisodeProvider>().Verify(x => x.GetPlaylistEpisodes(It.IsAny<Podcast>(),
            It.IsAny<YouTubePlaylistId>(), It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(),
            It.IsAny<bool>(), It.IsAny<PlaylistOrder?>(), It.IsAny<IEnumerable<string>?>()), times);

    [Fact(DisplayName =
        "With fresh hints a large playlist is read from the uploads head: the member found is built, no full walk runs, " +
        "the path is logged as 'playlist-uploads-head' and the hints take the new count and watermark.")]
    public async Task fresh_hints_use_uploads_head()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        var checkedThrough = _now.AddHours(-1);
        var videoId = _fixture.CreateYouTubeId();
        _uploadsHead = new YouTubeUploadsHeadResult([new PlaylistItem()], _options.UploadsHeadMinimumPlaylistItems + 51,
            checkedThrough, videoId, 1, null);
        _built = new GetPlaylistEpisodesResponse([episode]);

        // Act
        var response = await Run();

        // Assert
        response.Episodes.Should().Equal(episode);
        VerifyFullWalk(Times.Never());
        _logger.Entries.Should().Contain(e => e.Message.Contains("path='playlist-uploads-head'"));
        _podcast.YouTubeIndexingHints!.PlaylistItemCount.Should().Be(_options.UploadsHeadMinimumPlaylistItems + 51);
        _podcast.YouTubeIndexingHints.UploadsCheckedThrough.Should().Be(checkedThrough);
        _podcast.YouTubeIndexingHints.UploadsCheckedThroughVideoId.Should().Be(videoId);
        _podcast.YouTubeIndexingHints.LastFullWalk.Should().Be(_now.AddHours(-3));
    }

    [Fact(DisplayName =
        "When the uploads check cannot vouch for the playlist (a failed membership check) the pass walks in full and " +
        "the uploads watermark is unchanged, because unchecked uploads must be checked again.")]
    public async Task fallback_walks_in_full_and_keeps_watermark()
    {
        // Arrange
        var checkedThrough = _now.AddHours(-2);
        _podcast.YouTubeIndexingHints!.UploadsCheckedThrough = checkedThrough;
        _uploadsHead = YouTubeUploadsHeadResult.Fallback(YouTubeUploadsHeadCheck.MembershipUnknown, 1);

        // Act
        await Run();

        // Assert
        VerifyFullWalk(Times.Once());
        _podcast.YouTubeIndexingHints!.UploadsCheckedThrough.Should().Be(checkedThrough);
        _logger.Entries.Should().Contain(e =>
            e.Message.StartsWith(YouTubeEpisodeRetrievalHandler.HintsMessagePrefix) &&
            e.Message.Contains(YouTubeUploadsHeadCheck.MembershipUnknown));
    }

    [Fact(DisplayName =
        "A count change the uploads check cannot explain triggers a full walk, and the complete walk records a fresh " +
        "lastFullWalk and the new count.")]
    public async Task count_change_triggers_full_walk()
    {
        // Arrange
        var total = _options.UploadsHeadMinimumPlaylistItems + 60;
        _uploadsHead = YouTubeUploadsHeadResult.Fallback(YouTubeUploadsHeadCheck.PlaylistCountChanged);
        _walk = new GetPlaylistEpisodesResponse([], TotalResults: total,
            WalkOutcome: YouTubePlaylistWalkOutcome.Complete);

        // Act
        await Run();

        // Assert
        VerifyFullWalk(Times.Once());
        _podcast.YouTubeIndexingHints!.LastFullWalk.Should().Be(_now);
        _podcast.YouTubeIndexingHints.PlaylistItemCount.Should().Be(total);
    }

    [Fact(DisplayName =
        "After 24 hours the pass walks in full with no uploads check, even on a pass that skips expensive queries, " +
        "and a complete walk resets lastFullWalk.")]
    public async Task daily_full_walk_runs()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.LastFullWalk = _now.AddHours(-25);
        _walk = new GetPlaylistEpisodesResponse([], TotalResults: _options.UploadsHeadMinimumPlaylistItems + 50,
            WalkOutcome: YouTubePlaylistWalkOutcome.Complete);

        // Act
        await Run(new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true));

        // Assert
        VerifyFullWalk(Times.Once());
        _mocker.GetMock<IYouTubeUploadsHeadCheck>().VerifyNoOtherCalls();
        _podcast.YouTubeIndexingHints!.LastFullWalk.Should().Be(_now);
    }

    [Fact(DisplayName =
        "A full walk a page-walk guard stopped short does not set lastFullWalk, because a partial walk must not let " +
        "the next passes skip the playlist.")]
    public async Task guard_stopped_walk_does_not_set_last_full_walk()
    {
        // Arrange
        var lastFullWalk = _now.AddHours(-25);
        _podcast.YouTubeIndexingHints!.LastFullWalk = lastFullWalk;
        _walk = new GetPlaylistEpisodesResponse([], TotalResults: _options.UploadsHeadMinimumPlaylistItems + 50,
            WalkOutcome: YouTubePlaylistWalkOutcome.Incomplete);

        // Act
        await Run();

        // Assert
        _podcast.YouTubeIndexingHints!.LastFullWalk.Should().Be(lastFullWalk);
    }

    [Fact(DisplayName =
        "Hints written for another playlist are ignored: the pass walks in full and replaces them with hints for the " +
        "current playlist.")]
    public async Task mismatched_playlist_hints_are_ignored()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.PlaylistId = _fixture.CreateYouTubePlaylistId();
        _walk = new GetPlaylistEpisodesResponse([], TotalResults: _options.UploadsHeadMinimumPlaylistItems + 50,
            WalkOutcome: YouTubePlaylistWalkOutcome.Complete);

        // Act
        await Run();

        // Assert
        _mocker.GetMock<IYouTubeUploadsHeadCheck>().VerifyNoOtherCalls();
        _podcast.YouTubeIndexingHints!.PlaylistId.Should().Be(_podcast.YouTubePlaylistId);
        _podcast.YouTubeIndexingHints.LastFullWalk.Should().Be(_now);
    }

    [Fact(DisplayName =
        "A playlist at or below the threshold never uses the uploads check, and a complete walk clears its hints.")]
    public async Task below_threshold_has_no_uploads_head()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.PlaylistItemCount = _options.UploadsHeadMinimumPlaylistItems;
        _walk = new GetPlaylistEpisodesResponse([], TotalResults: _options.UploadsHeadMinimumPlaylistItems,
            WalkOutcome: YouTubePlaylistWalkOutcome.Complete);

        // Act
        await Run();

        // Assert
        _mocker.GetMock<IYouTubeUploadsHeadCheck>().VerifyNoOtherCalls();
        VerifyFullWalk(Times.Once());
        _podcast.YouTubeIndexingHints.Should().BeNull();
    }

    [Fact(DisplayName =
        "When every api-key is spent during the uploads check no full walk is attempted and the hints are unchanged, " +
        "because the walk could not run either.")]
    public async Task quota_exhausted_does_not_walk()
    {
        // Arrange
        var before = _podcast.YouTubeIndexingHints!.Copy();
        _uploadsHead = YouTubeUploadsHeadResult.Fallback(YouTubeUploadsHeadCheck.QuotaExhausted);

        // Act
        await Run();

        // Assert
        VerifyFullWalk(Times.Never());
        _podcast.YouTubeIndexingHints.Should().Be(before);
    }

    [Fact(DisplayName =
        "When building the uploads-head members fails the pass walks in full and the watermark stays, because hints " +
        "must not move past an upload that was not built.")]
    public async Task build_failure_walks_in_full()
    {
        // Arrange
        var before = _podcast.YouTubeIndexingHints!.Copy();
        _uploadsHead = new YouTubeUploadsHeadResult([new PlaylistItem()], before.PlaylistItemCount + 1,
            _now.AddHours(-1), _fixture.CreateYouTubeId(), 1, null);
        _built = new GetPlaylistEpisodesResponse(null);

        // Act
        await Run();

        // Assert
        VerifyFullWalk(Times.Once());
        _podcast.YouTubeIndexingHints.Should().Be(before);
    }
}
