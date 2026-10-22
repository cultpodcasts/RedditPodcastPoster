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
/// Playlist discovery cost on the hourly indexer: skip-expensive passes read the playlist head only, and stored videos
/// are not re-fetched, the same as channel discovery.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerPlaylistCostRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeEpisodeRetrievalHandler> _logger = new();
    private readonly Podcast _podcast;
    private IEnumerable<string>? _knownIds;
    private bool? _firstPageOnly;

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
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<bool>()))
            .Callback<Podcast, YouTubePlaylistId, YouTubeChannelId?, IndexingContext, bool, PlaylistOrder?,
                IEnumerable<string>?, bool>((_, _, _, _, _, _, knownIds, firstPageOnly) =>
            {
                _knownIds = knownIds;
                _firstPageOnly = firstPageOnly;
            })
            .ReturnsAsync(new GetPlaylistEpisodesResponse([]));
    }

    [Fact(DisplayName =
        "On a pass that skips expensive YouTube queries, playlist discovery asks for the head only and logs the " +
        "'playlist-head-only' path, because the label must say what the pass actually reads.")]
    public async Task skip_expensive_pass_reads_head_only()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _firstPageOnly.Should().BeTrue();
        _logger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith("YouTubeDiscoveryPath") && entry.Message.Contains("path='playlist-head-only'"));
    }

    [Fact(DisplayName =
        "On a pass that allows expensive YouTube queries, playlist discovery may walk the playlist and logs the " +
        "'playlist-paginated' path.")]
    public async Task expensive_pass_may_paginate()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: false);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _firstPageOnly.Should().BeFalse();
        _logger.Entries.Should().Contain(entry => entry.Message.Contains("path='playlist-paginated'"));
    }

    [Fact(DisplayName =
        "A playlist declared arbitrary-order is never limited to its head, because its capped full walk is the only " +
        "correct read of a curated playlist.")]
    public async Task arbitrary_playlist_is_not_limited()
    {
        // Arrange
        _podcast.YouTubePlaylistOrder = PlaylistOrder.Arbitrary;
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [], context);

        // Assert
        _firstPageOnly.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Playlist discovery passes the YouTube ids of stored in-window episodes as known ids, because videos the " +
        "podcast already has must not be looked up again (the same rule as channel discovery).")]
    public async Task stored_in_window_youtube_ids_are_known()
    {
        // Arrange
        var context = new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast);
        stored.ReleaseUtc = DomainTestFixture.UtcDaysAgo(1);
        var sut = _mocker.CreateInstance<YouTubeEpisodeRetrievalHandler>();

        // Act
        await sut.GetEpisodes(_podcast, [stored], context);

        // Assert
        _knownIds.Should().Contain(EpisodeServicePresence.YouTubeEpisodeId(stored));
    }
}
