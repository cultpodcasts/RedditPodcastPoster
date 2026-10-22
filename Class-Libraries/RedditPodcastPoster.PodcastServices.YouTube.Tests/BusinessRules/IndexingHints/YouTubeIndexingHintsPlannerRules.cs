using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.IndexingHints;

/// <summary>
/// When a pass may check the channel's uploads instead of walking a large playlist, and how a full walk changes the
/// YouTube indexing hints.
/// </summary>
public class YouTubeIndexingHintsPlannerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly YouTubeIndexingHintsOptions _options = new();
    private readonly Podcast _podcast;
    private readonly DateTime _now = DomainTestFixture.UtcDaysAgo(0).AddHours(12);
    private readonly IndexingContext _context =
        new(DomainTestFixture.UtcDaysAgo(2)) { UseYouTubeIndexingHints = true };

    public YouTubeIndexingHintsPlannerRules()
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
    }

    [Fact(DisplayName =
        "A large playlist with hints for the current playlist and a full walk younger than 24 hours is read from the " +
        "uploads head, because walking every page each hour is the cost the hints exist to avoid.")]
    public void fresh_hints_use_the_uploads_head()
    {
        // Arrange

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, _context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.UploadsHead);
        reason.Should().Be(YouTubeIndexingHintsPlanner.HintsUsable);
    }

    [Fact(DisplayName =
        "A playlist at or below the threshold (150 by default) is walked in full, because a short walk already costs " +
        "no more than the uploads check.")]
    public void playlist_at_threshold_is_walked()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.PlaylistItemCount = _options.UploadsHeadMinimumPlaylistItems;

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, _context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.BelowThreshold);
    }

    [Fact(DisplayName =
        "Once the last full walk is 24 hours old the pass walks the playlist in full, even when expensive queries are " +
        "skipped, because the daily walk catches anything the uploads check cannot see (an old video added to the " +
        "playlist while another was removed).")]
    public void daily_full_walk_runs_after_24_hours()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.LastFullWalk = _now - _options.FullWalkInterval;
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true)
            { UseYouTubeIndexingHints = true };

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.FullWalkDue);
    }

    [Fact(DisplayName =
        "Hints written for another playlist are ignored and the playlist is walked in full, because a curator who " +
        "swaps the playlist id makes the stored count and watermark meaningless.")]
    public void hints_for_another_playlist_are_ignored()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.PlaylistId = _fixture.CreateYouTubePlaylistId();

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, _context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.PlaylistChanged);
    }

    [Fact(DisplayName =
        "A pass without a date window walks the playlist in full, because the uploads check only finds recent uploads.")]
    public void no_window_walks_in_full()
    {
        // Arrange
        var context = new IndexingContext { UseYouTubeIndexingHints = true };

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.NoWindow);
    }

    [Fact(DisplayName =
        "A complete full walk of a large playlist records the walk's start, the count and the playlist id, and keeps " +
        "the uploads watermark, because the next passes check uploads from there.")]
    public void complete_walk_records_hints()
    {
        // Arrange
        var checkedThrough = _now.AddHours(-1);
        _podcast.YouTubeIndexingHints!.UploadsCheckedThrough = checkedThrough;
        var total = _options.UploadsHeadMinimumPlaylistItems + 7;

        // Act
        var hints = YouTubeIndexingHintsPlanner.AfterFullWalk(_podcast, YouTubePlaylistWalkOutcome.Complete, total,
            _now, _options);

        // Assert
        hints.Should().BeEquivalentTo(new YouTubeIndexingHints
        {
            PlaylistId = _podcast.YouTubePlaylistId,
            LastFullWalk = _now,
            PlaylistItemCount = total,
            UploadsCheckedThrough = checkedThrough
        });
    }

    [Fact(DisplayName =
        "A full walk a page-walk guard stopped short (or the arbitrary page cap tripped) leaves the hints unchanged and " +
        "does not set lastFullWalk, because a partial walk must not count as a full one.")]
    public void incomplete_walk_leaves_hints_unchanged()
    {
        // Arrange
        var before = _podcast.YouTubeIndexingHints!.Copy();

        // Act
        var hints = YouTubeIndexingHintsPlanner.AfterFullWalk(_podcast, YouTubePlaylistWalkOutcome.Incomplete,
            _options.UploadsHeadMinimumPlaylistItems + 99, _now, _options);

        // Assert
        hints.Should().Be(before);
    }

    [Fact(DisplayName =
        "A complete walk of a small playlist, or a newest-first walk that stopped at the window, clears the hints, " +
        "because those playlists are cheap to walk and the document stays clean.")]
    public void small_or_window_stopped_walk_clears_hints()
    {
        // Arrange

        // Act
        var small = YouTubeIndexingHintsPlanner.AfterFullWalk(_podcast, YouTubePlaylistWalkOutcome.Complete,
            _options.UploadsHeadMinimumPlaylistItems, _now, _options);
        var windowStopped = YouTubeIndexingHintsPlanner.AfterFullWalk(_podcast,
            YouTubePlaylistWalkOutcome.StoppedAtWindow, _options.UploadsHeadMinimumPlaylistItems + 99, _now, _options);

        // Assert
        small.Should().BeNull();
        windowStopped.Should().BeNull();
    }

    [Fact(DisplayName =
        "The uploads watermark is the later of the last full walk and the newest upload already checked.")]
    public void watermark_is_later_of_walk_and_checked_upload()
    {
        // Arrange
        var hints = _podcast.YouTubeIndexingHints!;
        hints.UploadsCheckedThrough = hints.LastFullWalk!.Value.AddMinutes(30);

        // Act
        var later = YouTubeIndexingHintsPlanner.Watermark(hints);
        hints.UploadsCheckedThrough = hints.LastFullWalk.Value.AddMinutes(-30);
        var walked = YouTubeIndexingHintsPlanner.Watermark(hints);

        // Assert
        later.Should().Be(hints.LastFullWalk.Value.AddMinutes(30));
        walked.Should().Be(hints.LastFullWalk);
    }

    [Fact(DisplayName =
        "A caller that did not opt in (Api 'index podcast', the Index console app) always walks the playlist in full, " +
        "even with fresh hints, because a curator re-index or a wide backfill must be able to recover an in-window " +
        "episode the uploads check would never offer again.")]
    public void caller_that_did_not_opt_in_walks_in_full()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(30));

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.NotOptedIn);
    }

    [Fact(DisplayName =
        "A pass that skips YouTube plans a full walk (which then does not run), because an uploads check would spend " +
        "quota the pass was told not to spend.")]
    public void skipped_youtube_does_not_use_the_uploads_head()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipYouTubeUrlResolving: true)
            { UseYouTubeIndexingHints = true };

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.SkipYouTube);
    }

    [Fact(DisplayName =
        "The daily full walk is due one hour early (23 hours by default), because passes run at the same minute each " +
        "day and a walk a few seconds short of 24 hours old must not slip to the next YouTube pass.")]
    public void daily_full_walk_is_due_within_the_slack()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.LastFullWalk = _now - _options.FullWalkInterval + TimeSpan.FromSeconds(5);

        // Act
        var (plan, reason) = YouTubeIndexingHintsPlanner.Plan(_podcast, _context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.FullWalk);
        reason.Should().Be(YouTubeIndexingHintsPlanner.FullWalkDue);
    }

    [Fact(DisplayName =
        "A full walk younger than the interval less the slack is still trusted, so the hourly passes in between use " +
        "the uploads head.")]
    public void walk_younger_than_the_slack_is_trusted()
    {
        // Arrange
        _podcast.YouTubeIndexingHints!.LastFullWalk =
            _now - _options.FullWalkDueAfter + TimeSpan.FromMinutes(1);

        // Act
        var (plan, _) = YouTubeIndexingHintsPlanner.Plan(_podcast, _context, _options, _now);

        // Assert
        plan.Should().Be(YouTubePlaylistDiscoveryPlan.UploadsHead);
    }
}
