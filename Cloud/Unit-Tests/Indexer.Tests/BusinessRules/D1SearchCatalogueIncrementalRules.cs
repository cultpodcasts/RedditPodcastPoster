using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class D1SearchCatalogueIncrementalRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public D1SearchCatalogueIncrementalRules()
    {
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(Array.Empty<Episode>().ToAsyncEnumerable());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(It.IsAny<Guid>()))
            .Returns(Array.Empty<Episode>().ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(Array.Empty<Podcast>().ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(Array.Empty<TvShowEpisode>().ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(Array.Empty<NewsReport>().ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(Array.Empty<Film>().ToAsyncEnumerable());
    }

    [Fact(DisplayName =
        "CountPlayables is the sum of the four playable container counts, because the full-scan catalogue bar needs " +
        "a total before the first page arrives.")]
    public async Task count_playables_sums_the_four_containers()
    {
        // Arrange
        _mocker.GetMock<IEpisodeRepository>().Setup(r => r.Count()).ReturnsAsync(10);
        _mocker.GetMock<ITvShowEpisodeRepository>().Setup(r => r.Count()).ReturnsAsync(3);
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.Count()).ReturnsAsync(2);
        _mocker.GetMock<IFilmRepository>().Setup(r => r.Count()).ReturnsAsync(4);
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var count = await sut.CountPlayables(CancellationToken.None);

        // Assert
        count.Should().Be(19);
    }

    [Fact(DisplayName =
        "ReadIncremental emits an older episode when only its podcast's _ts is in the window, because a podcast " +
        "rename or subject change must re-emit every episode document, and a same-window episode is only emitted once.")]
    public async Task incremental_includes_every_episode_of_a_changed_podcast()
    {
        // Arrange
        const long since = 100;
        var changed = _fixture.CreatePodcast();
        changed.Timestamp = 150;
        var unchanged = _fixture.CreatePodcast();
        unchanged.Timestamp = 10;
        var oldEpisode = _fixture.CreateStoredEpisode(changed, e => e.Timestamp = 50);
        var recentEpisode = _fixture.CreateStoredEpisode(unchanged, e => e.Timestamp = 120);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(new[] { recentEpisode }.ToAsyncEnumerable());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(changed.Id))
            .Returns(new[] { oldEpisode }.ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(unchanged.Id))
            .ReturnsAsync(unchanged);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(changed.Id))
            .ReturnsAsync(changed);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(new[] { changed }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var incremental = await sut.ReadIncremental(since, CancellationToken.None).ToListAsync();
        var windowOnly = await sut.Read(since, CancellationToken.None).ToListAsync();

        // Assert
        incremental.Select(c => c.Id).Should().BeEquivalentTo(new[]
        {
            recentEpisode.Id.ToString(), oldEpisode.Id.ToString()
        });
        windowOnly.Select(c => c.Id).Should().Equal(recentEpisode.Id.ToString());
        incremental.Single(c => c.Id == oldEpisode.Id.ToString()).Document!.ModifiedAt.Should().Be(150_000);
        incremental.Single(c => c.Id == recentEpisode.Id.ToString()).Document!.ModifiedAt.Should().Be(120_000);
    }

    [Fact(DisplayName =
        "ReadIncremental with a zero watermark does not expand every podcast, because that would read the whole " +
        "episode container twice on the first run.")]
    public async Task incremental_at_zero_does_not_expand_podcasts()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.Timestamp = 5;
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Timestamp = 5);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(new[] { episode }.ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(podcast.Id))
            .ReturnsAsync(podcast);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns(new[] { podcast }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut.ReadIncremental(0, CancellationToken.None).ToListAsync();

        // Assert
        candidates.Select(c => c.Id).Should().Equal(episode.Id.ToString());
        _mocker.GetMock<IPodcastRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.GetByPodcastId(It.IsAny<Guid>()), Times.Never);
    }
}
