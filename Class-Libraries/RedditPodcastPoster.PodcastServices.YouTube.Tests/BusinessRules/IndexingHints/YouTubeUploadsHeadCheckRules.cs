using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.IndexingHints;

/// <summary>
/// The uploads-head check of a large playlist against a fake YouTube API, through the real playlist, tolerant and
/// membership services. Quota is counted from the units those services record (the indexer's IndexerCostProbe
/// line is only a timing log, so it cannot measure units).
/// </summary>
public class YouTubeUploadsHeadCheckRules
{
    private const int StoredCount = 200;

    private readonly DomainTestFixture _fixture = new();
    private readonly FakeYouTubeHintsApi _api = new();
    private readonly IYouTubeServiceWrapper _wrapper;
    private readonly TolerantYouTubePlaylistService _playlistService;
    private readonly Podcast _podcast;
    private readonly YouTubePlaylistId _playlistId;
    private readonly string _uploadsId;
    private readonly YouTubeIndexingHints _hints;
    private readonly IndexingContext _context = new(DomainTestFixture.UtcDaysAgo(3));
    private readonly List<FakeYouTubeVideo> _playlist;
    private readonly List<FakeYouTubeVideo> _olderUploads;

    public YouTubeUploadsHeadCheckRules()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _playlistId = new YouTubePlaylistId(_podcast.YouTubePlaylistId);
        _uploadsId = YouTubeUploadsHeadCheck.UploadsPlaylistId(_podcast.YouTubeChannelId)!;
        _hints = new YouTubeIndexingHints
        {
            PlaylistId = _podcast.YouTubePlaylistId,
            LastFullWalk = DomainTestFixture.UtcDaysAgo(2),
            PlaylistItemCount = StoredCount
        };
        // An oldest-first curated playlist: a full walk must read every page.
        _playlist = Enumerable.Range(0, StoredCount)
            .Select(i => Video(DomainTestFixture.UtcDaysAgo(400).AddDays(i)))
            .ToList();
        _olderUploads = Enumerable.Range(0, 10).Select(i => Video(DomainTestFixture.UtcDaysAgo(5 + i))).ToList();
        _wrapper = _api.Wrapper();
        var inner = new YouTubePlaylistService(_api.QuotaTracker.Object, NullLogger<YouTubePlaylistService>.Instance);
        _playlistService = new TolerantYouTubePlaylistService(_wrapper, inner, _api.QuotaTracker.Object,
            NullLogger<TolerantYouTubePlaylistService>.Instance);
    }

    private FakeYouTubeVideo Video(DateTime publishedUtc) => new(_fixture.CreateYouTubeId(), publishedUtc);

    private YouTubeUploadsHeadCheck CreateSut() =>
        new(_playlistService,
            new YouTubePlaylistMembership(_api.QuotaTracker.Object, NullLogger<YouTubePlaylistMembership>.Instance),
            _wrapper,
            NullLogger<YouTubeUploadsHeadCheck>.Instance);

    private void Serve(IEnumerable<FakeYouTubeVideo> newUploadsNewestFirst, IEnumerable<FakeYouTubeVideo> addedToPlaylist)
    {
        _api.Playlist(_podcast.YouTubePlaylistId, _playlist.Concat(addedToPlaylist));
        _api.Playlist(_uploadsId, newUploadsNewestFirst.Concat(_olderUploads));
    }

    private Task<YouTubeUploadsHeadResult> Check(IReadOnlyCollection<string>? knownIds = null) =>
        CreateSut().Check(_podcast, _hints, _playlistId, knownIds ?? [], _context);

    [Fact(DisplayName =
        "A new upload that is in the playlist is found for 3 quota units (count, uploads, one membership check) and " +
        "the watermark moves to it, because the count rose by exactly the members found.")]
    public async Task new_member_upload_is_found()
    {
        // Arrange
        var upload = Video(DomainTestFixture.UtcDaysAgo(1));
        Serve([upload], [upload]);

        // Act
        var result = await Check();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Members.Select(x => x.Snippet.ResourceId.VideoId).Should().Equal(upload.Id);
        result.TotalResults.Should().Be(StoredCount + 1);
        result.CheckedThrough.Should().Be(upload.PublishedUtc);
        result.CheckedThroughVideoId.Should().Be(upload.Id);
        _api.QuotaUnits.Should().Be(3);
    }

    [Fact(DisplayName =
        "A new upload not in the playlist is skipped and the watermark moves past it for 2 quota units with no " +
        "membership check, because an unchanged playlist count means no upload joined it.")]
    public async Task non_member_upload_is_skipped_and_watermark_moves()
    {
        // Arrange
        var upload = Video(DomainTestFixture.UtcDaysAgo(1));
        Serve([upload], []);

        // Act
        var result = await Check();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Members.Should().BeEmpty();
        result.CheckedThrough.Should().Be(upload.PublishedUtc);
        _api.Requests.Should().NotContain(r => r.VideoId != null);
        _api.QuotaUnits.Should().Be(2);
    }

    [Fact(DisplayName =
        "With two new uploads and the count up by one, only the member is returned and the watermark moves past both, " +
        "because each new upload's membership is checked once.")]
    public async Task member_and_non_member_uploads()
    {
        // Arrange
        var member = Video(DomainTestFixture.UtcDaysAgo(1));
        var other = Video(DomainTestFixture.UtcDaysAgo(1).AddHours(2));
        Serve([other, member], [member]);

        // Act
        var result = await Check();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Members.Select(x => x.Snippet.ResourceId.VideoId).Should().Equal(member.Id);
        result.CheckedThrough.Should().Be(other.PublishedUtc);
        result.UploadsChecked.Should().Be(2);
        _api.QuotaUnits.Should().Be(4);
    }

    [Fact(DisplayName =
        "A playlist count that rose without a new member upload to explain it (an older video was added) falls back to a " +
        "full walk, because only the full walk can find that video.")]
    public async Task count_change_without_member_falls_back()
    {
        // Arrange
        var upload = Video(DomainTestFixture.UtcDaysAgo(1));
        Serve([upload], [Video(DomainTestFixture.UtcDaysAgo(90))]);

        // Act
        var result = await Check();

        // Assert
        result.FallbackReason.Should().Be(YouTubeUploadsHeadCheck.PlaylistCountChanged);
    }

    [Fact(DisplayName =
        "A membership check that fails falls back to a full walk with no new watermark, because an unchecked upload " +
        "must be checked again.")]
    public async Task failed_membership_falls_back()
    {
        // Arrange
        var upload = Video(DomainTestFixture.UtcDaysAgo(1));
        Serve([upload], [upload]);
        _api.MembershipFails = true;

        // Act
        var result = await Check();

        // Assert
        result.FallbackReason.Should().Be(YouTubeUploadsHeadCheck.MembershipUnknown);
        result.CheckedThrough.Should().BeNull();
    }

    [Fact(DisplayName =
        "When all 50 uploads read are newer than the watermark the check falls back to a full walk, because older " +
        "unchecked uploads may lie beyond the page.")]
    public async Task full_page_of_new_uploads_is_a_gap()
    {
        // Arrange
        var uploads = Enumerable.Range(0, YouTubeUploadsHeadCheck.UploadsPageSize)
            .Select(i => Video(DomainTestFixture.UtcDaysAgo(1).AddMinutes(-i)))
            .ToList();
        _api.Playlist(_podcast.YouTubePlaylistId, _playlist);
        _api.Playlist(_uploadsId, uploads);

        // Act
        var result = await Check();

        // Assert
        result.FallbackReason.Should().Be(YouTubeUploadsHeadCheck.UploadsGap);
    }

    [Fact(DisplayName =
        "Quota per path on a 201-item playlist: the uploads head costs 3 units where the full walk costs 5 pages, " +
        "because playlistItems.list costs 1 unit per call whatever its size.")]
    public async Task uploads_head_costs_less_than_full_walk()
    {
        // Arrange
        var upload = Video(DomainTestFixture.UtcDaysAgo(1));
        Serve([upload], [upload]);
        await Check();
        var uploadsHeadUnits = _api.QuotaUnits;

        // Act
        var walk = await _playlistService.GetPlaylistVideoSnippets(_playlistId, _context);
        var fullWalkUnits = _api.QuotaUnits - uploadsHeadUnits;

        // Assert
        uploadsHeadUnits.Should().Be(3);
        fullWalkUnits.Should().Be(5);
        walk.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Complete);
        walk.TotalResults.Should().Be(StoredCount + 1);
    }

    [Fact(DisplayName =
        "A channel id that is not a UC id has no derivable uploads playlist and falls back with no API call.")]
    public async Task non_uc_channel_falls_back()
    {
        // Arrange
        _podcast.YouTubeChannelId = "HC" + _podcast.YouTubeChannelId[2..];

        // Act
        var result = await Check();

        // Assert
        result.FallbackReason.Should().Be(YouTubeUploadsHeadCheck.NoUploadsPlaylist);
        _api.Requests.Should().BeEmpty();
    }
}
