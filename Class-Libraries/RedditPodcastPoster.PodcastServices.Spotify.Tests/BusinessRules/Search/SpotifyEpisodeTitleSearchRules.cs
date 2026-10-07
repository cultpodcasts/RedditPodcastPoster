using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Search;

/// <summary>
/// A single submitted episode looks up its Spotify counterpart with one title-search page
/// on one show. It must not page that show's catalogue.
/// </summary>
public class SpotifyEpisodeTitleSearchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SpotifyEpisodeTitleSearchRules()
    {
        _mocker.Use(NullLogger<SpotifyEpisodeTitleSearch>.Instance);
    }

    [Fact(DisplayName =
        "When the episode title is blank, FindCandidates does not call Spotify " +
        "because there is no query that can identify one episode.")]
    public async Task Blank_title_does_not_call_spotify()
    {
        // Arrange
        var sut = _mocker.CreateInstance<SpotifyEpisodeTitleSearch>();
        var request = CreateRequest(episodeTitle: string.Empty, released: DomainTestFixture.UtcDateDaysAgo(1));

        // Act
        var result = await sut.FindCandidates(request, new IndexingContext(), Market.CountryCode);

        // Assert
        result.Should().BeEmpty();
        VerifyFindEpisodes(Times.Never());
        VerifyGetSeveral(Times.Never());
        VerifyNoCatalogueWalk();
    }

    [Fact(DisplayName =
        "When the search page mixes the known Spotify show with other shows, FindCandidates keeps only that show " +
        "because one title search must not walk every show that shares the name.")]
    public async Task Show_id_keeps_only_that_show_from_one_search_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var showId = _fixture.CreateSpotifyId();
        var keptId = _fixture.CreateSpotifyId();
        var otherId = _fixture.CreateSpotifyId();
        var kept = CreateSimpleEpisode(keptId, title);
        var other = CreateSimpleEpisode(otherId, title);
        SearchRequest? captured = null;
        StubSearch([kept, other], search => captured = search);
        StubHydration(
        [
            CreateFullEpisode(keptId, title, showId, _fixture.CreateTitle()),
            CreateFullEpisode(otherId, title, _fixture.CreateSpotifyId(), _fixture.CreateTitle())
        ]);
        var sut = _mocker.CreateInstance<SpotifyEpisodeTitleSearch>();
        var request = CreateRequest(title, DomainTestFixture.UtcDateDaysAgo(800), podcastSpotifyId: showId);

        // Act
        var result = await sut.FindCandidates(request, new IndexingContext(), Market.CountryCode);

        // Assert
        result.Select(x => x.Id).Should().Equal(keptId);
        captured.Should().NotBeNull();
        captured!.Limit.Should().Be(SpotifyEpisodeTitleSearch.MaxResults);
        captured.Query.Should().Be(title);
        captured.Type.Should().Be(SearchRequest.Types.Episode);
        VerifyFindEpisodes(Times.Once());
        VerifyNoCatalogueWalk();
    }

    [Fact(DisplayName =
        "When no Spotify show id is known, FindCandidates keeps an exact show-name match and drops other names " +
        "because the submit still has to stay on one show.")]
    public async Task Exact_show_name_is_kept_when_show_id_is_missing()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var showName = _fixture.CreateTitle();
        var keptId = _fixture.CreateSpotifyId();
        var otherId = _fixture.CreateSpotifyId();
        StubSearch([CreateSimpleEpisode(keptId, title), CreateSimpleEpisode(otherId, title)]);
        StubHydration(
        [
            CreateFullEpisode(keptId, title, _fixture.CreateSpotifyId(), showName),
            CreateFullEpisode(otherId, title, _fixture.CreateSpotifyId(), _fixture.CreateTitle())
        ]);
        var sut = _mocker.CreateInstance<SpotifyEpisodeTitleSearch>();
        var request = CreateRequest(
            title,
            DomainTestFixture.UtcDateDaysAgo(2),
            podcastSpotifyId: string.Empty,
            podcastName: $"  {showName.ToUpperInvariant()}  ");

        // Act
        var result = await sut.FindCandidates(request, new IndexingContext(), Market.CountryCode);

        // Assert
        result.Select(x => x.Id).Should().Equal(keptId);
        VerifyFindEpisodes(Times.Once());
        VerifyNoCatalogueWalk();
    }

    [Fact(DisplayName =
        "When a years-old episode title is searched, FindCandidates still makes one search and does not page the show " +
        "because the release date is not a reason to download the catalogue.")]
    public async Task Years_old_release_still_uses_one_search_page()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        StubSearch([]);
        var sut = _mocker.CreateInstance<SpotifyEpisodeTitleSearch>();
        var request = CreateRequest(title, DomainTestFixture.UtcDateDaysAgo(800));

        // Act
        var result = await sut.FindCandidates(request, new IndexingContext(), Market.CountryCode);

        // Assert
        result.Should().BeEmpty();
        VerifyFindEpisodes(Times.Once());
        VerifyGetSeveral(Times.Never());
        VerifyNoCatalogueWalk();
    }

    private FindSpotifyEpisodeRequest CreateRequest(
        string episodeTitle,
        DateTime? released,
        string? podcastSpotifyId = null,
        string? podcastName = null) =>
        new(
            PodcastSpotifyId: podcastSpotifyId ?? _fixture.CreateSpotifyId(),
            PodcastName: podcastName ?? _fixture.CreateTitle(),
            EpisodeSpotifyId: string.Empty,
            EpisodeTitle: episodeTitle,
            Released: released,
            HasExpensiveSpotifyEpisodesQuery: false);

    private void StubSearch(IList<SimpleEpisode> hits, Action<SearchRequest>? capture = null)
    {
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.FindEpisodes(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<SearchRequest, IndexingContext, CancellationToken>((request, _, _) => capture?.Invoke(request))
            .ReturnsAsync(new Paging<SimpleEpisode, SearchResponse> { Items = hits.ToList() });
    }

    private void StubHydration(IList<FullEpisode> episodes)
    {
        _mocker.GetMock<ISpotifyClientWrapper>()
            .Setup(x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodesResponse { Episodes = episodes.ToList() });
    }

    private SimpleEpisode CreateSimpleEpisode(string id, string title) =>
        new()
        {
            Id = id,
            Name = title,
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = true
        };

    private FullEpisode CreateFullEpisode(string id, string title, string showId, string showName) =>
        new()
        {
            Id = id,
            Name = title,
            IsPlayable = true,
            Show = new SimpleShow { Id = showId, Name = showName }
        };

    private void VerifyFindEpisodes(Times times) =>
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(
            x => x.FindEpisodes(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            times);

    private void VerifyGetSeveral(Times times) =>
        _mocker.GetMock<ISpotifyClientWrapper>().Verify(
            x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            times);

    private void VerifyNoCatalogueWalk()
    {
        var client = _mocker.GetMock<ISpotifyClientWrapper>();
        client.Verify(
            x => x.PaginateAll(
                It.IsAny<IPaginatable<SimpleEpisode, SearchResponse>>(),
                It.IsAny<Func<SearchResponse, IPaginatable<SimpleEpisode, SearchResponse>>>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
        client.Verify(
            x => x.PaginateAll(
                It.IsAny<IPaginatable<SimpleEpisode>>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
        client.Verify(
            x => x.GetShowEpisodes(
                It.IsAny<string>(),
                It.IsAny<ShowEpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
