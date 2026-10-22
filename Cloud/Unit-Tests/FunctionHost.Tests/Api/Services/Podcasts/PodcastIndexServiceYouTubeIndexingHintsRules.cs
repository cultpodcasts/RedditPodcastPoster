using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastIndexServiceYouTubeIndexingHintsRules
{
    private readonly AutoMocker _mocker = new();
    private IndexingContext? _context;

    public PodcastIndexServiceYouTubeIndexingHintsRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 4 }));
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Index(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .Callback<string, IndexingContext>((_, context) => _context = context);
    }

    [Fact(DisplayName =
        "Api 'index podcast' does not opt in to YouTube indexing hints, so a curator re-index always walks the " +
        "playlist in full and can recover an in-window episode the uploads check would not offer again.")]
    public async Task curator_index_walks_youtube_playlists_in_full()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        await sut.IndexAsync("synthetic-podcast", CancellationToken.None);

        // Assert
        _context.Should().NotBeNull();
        _context!.UseYouTubeIndexingHints.Should().BeFalse();
    }
}
