using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1479: an incremental reconcile reads eligible ids missing from D1 outside the <c>_ts</c> window by id, with the
/// same eligibility and document rules as a full read.
/// </summary>
public class D1SearchCatalogueReadByIdsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public D1SearchCatalogueReadByIdsRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<ILogger<D1SearchCatalogue>>(NullLogger<D1SearchCatalogue>.Instance);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(AsyncEnumerable.Empty<TvShowEpisode>());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(AsyncEnumerable.Empty<NewsReport>());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(AsyncEnumerable.Empty<Film>());
    }

    [Fact(DisplayName =
        "Reading by id returns a document for an eligible episode whatever its _ts, no document for a removed one, " +
        "and nothing for an id no container holds, so the reconcile can tell missing from no-longer-eligible (#1479).")]
    public async Task read_by_ids_applies_the_full_rules()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        var live = Episode(podcast, removed: false);
        var removed = Episode(podcast, removed: true);
        var unrelated = Episode(podcast, removed: false);
        _episodes.Seed(live, removed, unrelated);
        var gone = _fixture.CreateGuid().ToString();
        var sut = _mocker.CreateInstance<D1SearchCatalogue>();

        // Act
        var candidates = await sut
            .ReadByIds([live.Id.ToString(), removed.Id.ToString(), gone], CancellationToken.None)
            .ToListAsync();

        // Assert
        candidates.Select(c => c.Id).Should().BeEquivalentTo([live.Id.ToString(), removed.Id.ToString()]);
        candidates.Single(c => c.Id == live.Id.ToString()).Document.Should().NotBeNull();
        candidates.Single(c => c.Id == removed.Id.ToString()).Excluded.Should().BeTrue();
    }

    private Episode Episode(RedditPodcastPoster.Models.Podcasts.Podcast podcast, bool removed)
    {
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        episode.Removed = removed;
        episode.ParentRemoved = null;
        return episode;
    }
}
