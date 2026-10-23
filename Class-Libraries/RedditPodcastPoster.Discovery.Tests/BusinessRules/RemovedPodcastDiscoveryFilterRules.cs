using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Discovery.Adapters;
using RedditPodcastPoster.Discovery.Enrichers;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Discovery.Providers;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using AutoFixture;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Discovery.ML.Models;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

/// <summary>
/// Removed podcasts use the same discovery elimination as fully indexed shows.
/// </summary>
public class RemovedPodcastDiscoveryFilterRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Podcast> _podcasts = [];

    public RemovedPodcastDiscoveryFilterRules()
    {
        _mocker.Use<IEpisodeResultEnricher>(new EpisodeResultEnricher());
        _mocker.GetMock<IIgnoreTermsProvider>().Setup(p => p.GetIgnoreTerms()).Returns([]);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) => ToAsync(_podcasts.Where(predicate.Compile())));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<RedditPodcastPoster.Models.News.NewsOrganisation, bool>>>()))
            .Returns(ToAsync(Array.Empty<RedditPodcastPoster.Models.News.NewsOrganisation>()));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<RedditPodcastPoster.Models.TvShows.TvShow, bool>>>()))
            .Returns(ToAsync(Array.Empty<RedditPodcastPoster.Models.TvShows.TvShow>()));
    }

    [Fact(DisplayName =
        "A discovered episode whose catalogue podcast is removed is left out of the discovery results, because a banned channel must not surface on a later run.")]
    public async Task removed_podcast_is_eliminated()
    {
        // Arrange
        var enriched = new EnrichedEpisodeResult(Episode(), [new PodcastResult(_fixture.Create<Guid>(), false, true)]);
        var sut = _mocker.CreateInstance<EnrichedEpisodeResultsAdapter>();

        // Act
        var results = await Collect(sut.ToDiscoveryResults(One(enriched)));

        // Assert
        results.Should().BeEmpty();
        _mocker.GetMock<IDiscoveryResultScorer>().Verify(s => s.Score(It.IsAny<DiscoveryResult>()), Times.Never);
    }

    [Fact(DisplayName =
        "A discovered episode from a podcast that is not removed and does not index every episode is still returned, because the removed check is the same elimination as the indexed-show check.")]
    public async Task active_unindexed_podcast_is_kept()
    {
        // Arrange
        var enriched = new EnrichedEpisodeResult(Episode(), [new PodcastResult(_fixture.Create<Guid>(), false, false)]);
        var scored = new DiscoveryResult { Id = _fixture.Create<Guid>() };
        _mocker.GetMock<IEnrichedEpisodeResultAdapter>()
            .Setup(a => a.ToDiscoveryResult(It.IsAny<EnrichedEpisodeResult>()))
            .ReturnsAsync(scored);
        _mocker.GetMock<IDiscoveryResultScorer>()
            .Setup(s => s.Score(scored))
            .Returns(new DiscoveryScoreResult(0.5f, false));
        var sut = _mocker.CreateInstance<EnrichedEpisodeResultsAdapter>();

        // Act
        var results = await Collect(sut.ToDiscoveryResults(One(enriched)));

        // Assert
        results.Should().ContainSingle().Which.Should().BeSameAs(scored);
    }

    [Fact(DisplayName =
        "When one podcast on a show id indexes every episode and another with the same id is removed, the removed podcast is still marked for elimination, because banning must not depend on which match is the indexed one.")]
    public async Task removed_match_is_kept_beside_the_indexed_match()
    {
        // Arrange
        var spotifyId = _fixture.Create<string>();
        var indexed = new Podcast
        {
            Id = _fixture.Create<Guid>(),
            Name = _fixture.Create<string>(),
            SpotifyId = spotifyId,
            IndexAllEpisodes = true
        };
        var removed = new Podcast
        {
            Id = _fixture.Create<Guid>(),
            Name = _fixture.Create<string>(),
            SpotifyId = spotifyId,
            Removed = true
        };
        _podcasts.AddRange([indexed, removed]);
        var episode = Episode();
        episode.PodcastIds.Spotify = spotifyId;
        var sut = _mocker.CreateInstance<EpisodeResultsEnricher>();

        // Act
        var enriched = await Collect(sut.EnrichWithPodcastDetails([episode]));

        // Assert
        var matches = enriched.Should().ContainSingle().Subject.PodcastResults;
        matches.Should().Contain(x => x.PodcastId == removed.Id && x.Removed);
        matches.Should().Contain(x => x.PodcastId == indexed.Id && x.IndexAllEpisodes);
    }

    private EpisodeResult Episode() =>
        new(
            _fixture.Create<string>(),
            _fixture.Create<DateTime>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            null,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DiscoverService.YouTube);

    private static async IAsyncEnumerable<EnrichedEpisodeResult> One(EnrichedEpisodeResult item)
    {
        yield return item;
        await Task.CompletedTask;
    }

    private static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> items)
    {
        var list = new List<T>();
        await foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}
