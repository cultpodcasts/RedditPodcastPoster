using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1484: the D1 reconcile catalogue agrees with the D1 mirror on episodes held for approval: the eligible-id count
/// (#1479) leaves them out and a reconcile read never offers a document for one, whatever its removed flag says.
/// </summary>
public class D1SearchCatalogueApprovalRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly Episode _ordinary;
    private readonly Episode _awaiting;
    private readonly Episode _rejected;

    public D1SearchCatalogueApprovalRules()
    {
        var podcast = _fixture.CreatePodcast();
        _podcasts.Seed(podcast);
        _ordinary = Live(podcast, null);
        _awaiting = Live(podcast, ApprovalStatus.AwaitingApproval);
        _rejected = Live(podcast, ApprovalStatus.Rejected);
        _episodes.Seed(_ordinary, _awaiting, _rejected);
    }

    private Episode Live(RedditPodcastPoster.Models.Podcasts.Podcast podcast, ApprovalStatus? status)
    {
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        // Removed cleared on purpose: held must exclude on its own.
        episode.Removed = false;
        episode.ParentRemoved = null;
        episode.ApprovalStatus = status;
        return episode;
    }

    private D1SearchCatalogue Catalogue()
    {
        // TV, news and film repositories are auto-mocked and return empty streams.
        var mocker = new AutoMocker();
        var empty = new EmptyAsyncEnumerables();
        mocker.GetMock<ITvShowEpisodeRepository>().DefaultValueProvider = empty;
        mocker.GetMock<INewsReportRepository>().DefaultValueProvider = empty;
        mocker.GetMock<IFilmRepository>().DefaultValueProvider = empty;
        mocker.Use<IEpisodeRepository>(_episodes);
        mocker.Use<IPodcastRepository>(_podcasts);
        mocker.Use<Microsoft.Extensions.Logging.ILogger<D1SearchCatalogue>>(NullLogger<D1SearchCatalogue>.Instance);
        return mocker.CreateInstance<D1SearchCatalogue>();
    }

    [Fact(DisplayName =
        "The D1 eligible-id count leaves out an episode awaiting approval or rejected, even with removed cleared, so " +
        "it matches the D1 mirror and the #1479 count check never reports a permanent mismatch (#1484).")]
    public async Task eligible_ids_skip_held_episodes()
    {
        // Arrange
        var sut = Catalogue();

        // Act
        var ids = await sut.ReadEligibleIds(CancellationToken.None).ToListAsync();

        // Assert
        ids.Should().BeEquivalentTo([_ordinary.Id.ToString()]);
    }

    [Fact(DisplayName =
        "A D1 reconcile read offers no document for an episode awaiting approval or rejected (it is excluded, so a " +
        "reconcile write deletes rather than adds it), and a document for an ordinary episode (#1484).")]
    public async Task reconcile_read_never_adds_held_episodes()
    {
        // Arrange
        var sut = Catalogue();

        // Act
        var candidates = await sut.Read(0, CancellationToken.None).ToListAsync();

        // Assert
        candidates.Single(c => c.Id == _ordinary.Id.ToString()).Document.Should().NotBeNull();
        candidates.Single(c => c.Id == _awaiting.Id.ToString()).Document.Should().BeNull();
        candidates.Single(c => c.Id == _rejected.Id.ToString()).Document.Should().BeNull();
    }

    /// <summary>Unset repository calls return an empty async stream instead of null.</summary>
    private sealed class EmptyAsyncEnumerables : LookupOrFallbackDefaultValueProvider
    {
        private static readonly MethodInfo NoneMethod =
            typeof(EmptyAsyncEnumerables).GetMethod(nameof(None), BindingFlags.NonPublic | BindingFlags.Static)!;

        public EmptyAsyncEnumerables()
        {
            Register(typeof(IAsyncEnumerable<>),
                (type, _) => NoneMethod.MakeGenericMethod(type.GetGenericArguments()[0]).Invoke(null, null)!);
        }

        private static async IAsyncEnumerable<T> None<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
