using Azure.Search.Documents;
using RemoveEpisodes.Tests.Fakes;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Subjects.Factories;
using System.Linq.Expressions;
using SearchDocument = RedditPodcastPoster.Search.Models.SearchDocument;

namespace AddSubjectToSearchMatches.Tests.BusinessRules;

public class AddSubjectToSearchMatchesSearchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly FakeEpisodeSearchIndex _index = new();
    private readonly Mock<SearchClient> _search;
    private readonly Subject _subject;
    private readonly Podcast _podcast;
    private readonly Podcast _otherPodcast;
    private readonly Episode _episode;
    private readonly Episode _newsReport;

    public AddSubjectToSearchMatchesSearchRules()
    {
        _subject = new Subject(_fixture.CreateTitle());
        _podcast = _fixture.CreatePodcast();
        _otherPodcast = _fixture.CreatePodcast();
        _episode = _fixture.CreateStoredEpisode(_podcast);
        _newsReport = _fixture.CreateStoredEpisode(_podcast);

        // AutoMocker cannot construct SearchClient (it picks the settings constructor and fails on a null endpoint),
        // so the client mock comes from Moq's protected parameterless constructor and is registered with AutoMocker.
        var searchClient = Mock.Of<SearchClient>();
        _mocker.Use(searchClient);
        _search = Mock.Get(searchClient);
        _search
            .Setup(x => x.SearchAsync<SearchDocument>(It.IsAny<string>(), It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, SearchOptions options, CancellationToken _) => _index.Search(options));
        _mocker.GetMock<ISubjectsProvider>()
            .Setup(x => x.GetAll())
            .Returns(() => new[] { _subject }.ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) =>
                new[] { _podcast, _otherPodcast }.Where(predicate.Compile()).ToAsyncEnumerable());
        var episodes = _mocker.GetMock<IEpisodeRepository>();
        episodes.Setup(x => x.GetEpisode(_podcast.Id, _episode.Id)).ReturnsAsync(() => _episode);
        episodes.Setup(x => x.GetEpisode(_podcast.Id, _newsReport.Id)).ReturnsAsync(() => _newsReport);
    }

    private Request Query() => new()
    {
        Query = _subject.Name,
        Throttle = _index.Count,
        AddSubjectWhenNotSubjectMatch = true
    };

    [Fact(DisplayName =
        "Add subject to search matches CLI: when it searches the episode index, then the search is accepted, " +
        "because every field it selects is a retrievable field of the index.")]
    public async Task search_selects_only_fields_the_index_has()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _fixture.CreateTitle(), _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        var act = () => sut.Process(Query());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Add subject to search matches CLI: when a matching episode is found in the index, then the subject is " +
        "added to the episode of the podcast named by the document's series name and the episode is re-indexed, " +
        "and the subject matcher is given the title and description held in the index, because the series name " +
        "is the podcast name and the title and description are the episode's.")]
    public async Task matching_episode_gets_the_subject_from_the_podcast_named_by_series_name()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var description = _fixture.CreateTitle();
        _index.Add(SearchContentKind.Episode, _episode.Id, title, description, _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        await sut.Process(Query());

        // Assert
        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>().Verify(x => x.CreateAsync(
            It.Is<Podcast>(p => p.Id == _podcast.Id),
            It.Is<Episode>(e => e.Id == _episode.Id && e.Title == title && e.Description == description)));
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Id == _episode.Id && e.Subjects.Contains(_subject.Name))),
                Times.Once);
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(x => x.IndexEpisodes(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { _episode.Id })),
            It.IsAny<CancellationToken>()));
    }

    [Fact(DisplayName =
        "Add subject to search matches CLI: when an episode document has no series name, then it is skipped and " +
        "a warning names its id, because without a series name its podcast cannot be found.")]
    public async Task episode_without_series_name_is_skipped_and_logged()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _fixture.CreateTitle(), _fixture.CreateTitle(),
            null, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        await sut.Process(Query());

        // Assert
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
        _mocker.GetMock<IEpisodeSearchIndexerService>().Verify(
            x => x.IndexEpisodes(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ILogger<Processor>>().Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains(_episode.Id.ToString())),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact(DisplayName =
        "Add subject to search matches CLI: when the index also holds a news report that matches the query, then " +
        "only the episode gets the subject, because the index holds every content kind and this tool tags " +
        "episodes.")]
    public async Task only_episode_kind_documents_get_the_subject()
    {
        // Arrange
        _index.Add(SearchContentKind.Episode, _episode.Id, _fixture.CreateTitle(), _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        _index.Add(SearchContentKind.NewsReport, _newsReport.Id, _fixture.CreateTitle(), _fixture.CreateTitle(),
            _podcast.Name, _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<Processor>();

        // Act
        await sut.Process(Query());

        // Assert
        _newsReport.Subjects.Should().NotContain(_subject.Name);
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(x => x.Save(It.Is<Episode>(e => e.Id == _newsReport.Id)), Times.Never);
    }
}