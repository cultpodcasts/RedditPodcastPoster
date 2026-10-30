using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Handlers;

/// <summary>
/// Playlist discovery cost on the hourly indexer: stored videos are not re-fetched (the same rule as channel discovery),
/// unless merge would still fill something in on them; every non-arbitrary playlist still pages as before.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerPlaylistCostRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeRetrievalHandler> _logger = new();
    private readonly Podcast _podcast;
    private IEnumerable<string>? _knownIds;
    private GetPlaylistEpisodesResponse _response = new([]);

    public YouTubeEpisodeRetrievalHandlerPlaylistCostRules()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _mocker.Use<ILogger<YouTubeEpisodeRetrievalHandler>>(_logger);
        _mocker.GetMock<IYouTubeEpisodeProvider>()
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>(),
                It.IsAny<IEnumerable<string>?>()))
            .Callback<Podcast, YouTubePlaylistId, IndexingContext, bool, PlaylistOrder?,
                IEnumerable<string>?>((_, _, _, _, _, knownIds) => _knownIds = knownIds)
            .ReturnsAsync(() => _response);
    }

    [Fact(DisplayName =
        "On a pass that skips expensive YouTube queries a non-arbitrary playlist is still paginated and logged as " +
        "'playlist-paginated', because reading only the head would never find new uploads at the tail of an " +
        "oldest-first playlist.")]
    public async Task skip_expensive_pass_still_paginates()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _logger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith("YouTubeDiscoveryPath") && entry.Message.Contains("path='playlist-paginated'"));
    }

    [Fact(DisplayName =
        "An arbitrary-order playlist logs the 'playlist-arbitrary-full-walk' path.")]
    public async Task arbitrary_playlist_logs_full_walk()
    {
        // Arrange
        _podcast.YouTubePlaylistOrder = PlaylistOrder.Arbitrary;
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _logger.Entries.Should().Contain(entry => entry.Message.Contains("path='playlist-arbitrary-full-walk'"));
    }

    [Fact(DisplayName =
        "Playlist discovery passes the YouTube id of a complete stored episode as known, because videos the podcast " +
        "already has must not be looked up again (the same rule as channel discovery).")]
    public async Task complete_stored_episode_is_known()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        var stored = CompleteStoredEpisode(DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()));
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [stored], context);

        // Assert
        _knownIds.Should().Contain(EpisodeServicePresence.YouTubeEpisodeId(stored));
    }

    [Fact(DisplayName =
        "A stored episode released before the window (but returned by the release-scoped repository read) is still " +
        "known, because playlist items are windowed on the later of added-at and published-at, so an old video newly " +
        "added to the playlist must not be re-fetched on every pass.")]
    public async Task stored_episode_released_before_window_is_known()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        var stored = CompleteStoredEpisode(DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay()));
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [stored], context);

        // Assert
        _knownIds.Should().Contain(EpisodeServicePresence.YouTubeEpisodeId(stored));
    }

    [Fact(DisplayName =
        "A stored playlist episode missing its YouTube image is not treated as known, so discovery re-fetches it and " +
        "merge fills the image in, because enrichment does not fill an image on an episode that already has its " +
        "YouTube id.")]
    public async Task stored_episode_missing_youtube_image_is_not_known()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        var stored = CompleteStoredEpisode(
            DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()), withYouTubeImage: false);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [stored], context);

        // Assert
        _knownIds.Should().NotContain(EpisodeServicePresence.YouTubeEpisodeId(stored));
    }

    [Fact(DisplayName =
        "A stored playlist episode with a date-only (midnight) release is not treated as known, so discovery re-fetches " +
        "it and merge backfills the release time from YouTube.")]
    public async Task stored_episode_with_date_only_release_is_not_known()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        var stored = CompleteStoredEpisode(DomainTestFixture.UtcDaysAgo(1));
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [stored], context);

        // Assert
        _knownIds.Should().NotContain(EpisodeServicePresence.YouTubeEpisodeId(stored));
    }

    [Fact(DisplayName =
        "The playlist discovery path log reports the known videos it skipped alongside the new ones found, because " +
        "episodes-found now counts only new videos and in-window = episodes-found + known-skipped.")]
    public async Task discovery_path_log_reports_known_skipped()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        var knownSkipped = _fixture.CreateDuration().Minutes + 1;
        _response = new GetPlaylistEpisodesResponse([], KnownSkipped: knownSkipped);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _logger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith("YouTubeDiscoveryPath") &&
            entry.Message.Contains("episodes-found='0'") &&
            entry.Message.Contains($"known-skipped='{knownSkipped}'"));
    }

    private RedditPodcastPoster.Models.Episodes.Episode CompleteStoredEpisode(
        DateTime release,
        bool withYouTubeImage = true)
    {
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast, release);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(stored)!;
        if (withYouTubeImage)
        {
            EpisodeServicePresence.Upsert(stored, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(youTubeId),
                new Uri($"https://i.ytimg.com/vi/{youTubeId}/maxresdefault.jpg"));
        }

        stored.Description = _fixture.CreateTitle();
        return stored;
    }
}
