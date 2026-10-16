using System.Linq.Expressions;
using Azure.Search.Documents;
using CliSearchTests.Fakes;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using SearchDocument = RedditPodcastPoster.Search.Models.SearchDocument;

namespace RemoveEpisodes.Tests.BusinessRules;

public class RemoveEpisodesSearchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeEpisodeSearchIndex _index = new();
    private readonly Mock<SearchClient> _search = new();
    private readonly Podcast _podcast;
    private readonly Podcast _otherPodcast;
    private readonly Episode _episode;
    private readonly Episode _newsReport;

    public RemoveEpisodesSearchRules()
    {
        _podcast = _fixture.CreatePodcast();
        _otherPodcast = _fixture.CreatePodcast();
        _episode = _fixture.CreateStoredEpisode(_podcast);
        _newsReport = _fixture.CreateStoredEpisode(_podcast);

        _mocker.Use(_search);
        _search
            .Setup(x => x.SearchAsync<SearchDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, SearchOptions options, CancellationToken _) => _index.Search(options));
        _search
            .Setup(x => x.DeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Azure.Response.FromValue(
                Azure.Search.Documents.Models.SearchModelFactory.IndexDocumentsResult(
                [
                    Azure.Search.Documents.Models.SearchModelFactory.IndexingResult(
                        _episode.Id.ToString(), null, true, 200)
                ]), Mock.Of<Azure.Response>()));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) =>
                new[] { _podcast, _otherPodcast }.Where(predicate.Compile()).ToAsyncEnumerable());
        var episodes = _mocker.GetMock<IEpisodeRepository>();
        episodes.Setup(x => x.GetEpisode(_podcast.Id, _episode.Id)).ReturnsAsync(() => _episode);
        episodes.Setup(x => x.GetEpisode(_podcast.Id, _newsReport.Id)).ReturnsAsync(() => _newsReport);
    }

    private RemoveRequest Query() => new()
    {
        Query = _fixture.CreateTitle(),
        Throttle = 5,
        IsNonDryRun = true
    };

    [Fact(DisplayName =
        "Remove episodes CLI: when it searches the episode index, then the search is accepted, because every " +
        "field it selects is a retrievable field of the index.")]
    public async Task search_selects_only_fields_the_index_has()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _episode.Title, _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var act = () => sut.Process(Query());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Remove episodes CLI: when a matching episode is found in the index, then the episode of the podcast named " +
        "by the document's series name is marked removed and deleted from the index, because the series name is " +
        "the podcast name.")]
    public async Task matching_episode_is_removed_from_the_podcast_named_by_series_name()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _episode.Title, _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        await sut.Process(Query());

        // Assert
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Id == _episode.Id && e.Removed)), Times.Once);
        _search.Verify(x => x.DeleteDocumentsAsync("id",
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { _episode.Id.ToString() })),
            It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Remove episodes CLI: when the index also holds a news report that matches the query, then only the " +
        "episode is removed, because the index holds every content kind and this tool removes episodes.")]
    public async Task only_episode_kind_documents_are_removed()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _episode.Title, _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        _index.Add(SearchContentKind.NewsReport, _newsReport.Id, _fixture.CreateTitle(), _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        await sut.Process(Query());

        // Assert
        _newsReport.Removed.Should().BeFalse();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Id == _newsReport.Id)), Times.Never);
        _search.Verify(x => x.DeleteDocumentsAsync("id",
            It.Is<IEnumerable<string>>(ids => ids.Contains(_newsReport.Id.ToString())),
            It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}