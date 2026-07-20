using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// Indexer SpotifyEpisodeProvider post-filters by ReleasedSince then maps SimpleEpisode â†’ Episode,
/// preserving ExpensiveQueryFound from the inner catalogue provider.
/// </summary>
public class SpotifyEpisodeProviderRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When ReleasedSince is unset, GetEpisodes maps every SimpleEpisode from the inner provider " +
        "because an unscoped indexer pass must retain the full recent catalogue page.")]
    public async Task No_released_since_maps_all_inner_episodes()
    {
        // Arrange
        var newer = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1));
        var older = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(10));
        var inner = CreateInnerProvider([newer, older], expensive: false);
        var sut = CreateSut(inner.Object);
        var request = new GetEpisodesRequest(new SpotifyPodcastId(_fixture.CreateSpotifyId()), Market: null);

        // Act
        var result = await sut.GetEpisodes(request, new IndexingContext());

        // Assert
        result.Results.Should().HaveCount(2);
        result.Results!.Select(x => x.SpotifyId).Should().BeEquivalentTo(newer.Id, older.Id);
        result.ExpensiveQueryFound.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When ReleasedSince is set, GetEpisodes excludes SimpleEpisodes older than that boundary " +
        "because the indexer must not map stale catalogue rows past the slot window.")]
    public async Task Released_since_excludes_older_episodes()
    {
        // Arrange
        var included = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1));
        var excluded = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(10));
        var inner = CreateInnerProvider([included, excluded], expensive: false);
        var sut = CreateSut(inner.Object);
        var request = new GetEpisodesRequest(new SpotifyPodcastId(_fixture.CreateSpotifyId()), Market: null);
        var indexingContext = new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var result = await sut.GetEpisodes(request, indexingContext);

        // Assert
        result.Results.Should().ContainSingle();
        result.Results![0].SpotifyId.Should().Be(included.Id);
    }

    [Fact(DisplayName =
        "When the inner provider reports ExpensiveQueryFound, GetEpisodes preserves that flag on the response " +
        "because the retrieval handler must mark the podcast for expensive-query guards.")]
    public async Task Expensive_query_found_is_passthrough()
    {
        // Arrange
        var episode = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1));
        var inner = CreateInnerProvider([episode], expensive: true);
        var sut = CreateSut(inner.Object);
        var request = new GetEpisodesRequest(new SpotifyPodcastId(_fixture.CreateSpotifyId()), Market: null);

        // Act
        var result = await sut.GetEpisodes(request, new IndexingContext());

        // Assert
        result.ExpensiveQueryFound.Should().BeTrue();
        result.Results.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the inner provider returns no episodes, GetEpisodes returns an empty Results list " +
        "because an empty catalogue page must not invent indexed rows.")]
    public async Task Empty_inner_list_returns_empty_results()
    {
        // Arrange
        var inner = CreateInnerProvider([], expensive: false);
        var sut = CreateSut(inner.Object);
        var request = new GetEpisodesRequest(new SpotifyPodcastId(_fixture.CreateSpotifyId()), Market: null);

        // Act
        var result = await sut.GetEpisodes(request, new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        result.Results.Should().BeEmpty();
        result.ExpensiveQueryFound.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a SimpleEpisode has IsPlayable=false, GetEpisodes excludes it from mapped results " +
        "because paywalled Spotify episodes must not be indexed via the Spotify provider.")]
    public async Task Non_playable_episode_is_excluded_from_mapped_results()
    {
        // Arrange
        var free = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1), isPlayable: true);
        var paywalled = CreateSimpleEpisode(DomainTestFixture.UtcDateDaysAgo(1), isPlayable: false);
        var inner = CreateInnerProvider([free, paywalled], expensive: false);
        var sut = CreateSut(inner.Object);
        var request = new GetEpisodesRequest(new SpotifyPodcastId(_fixture.CreateSpotifyId()), Market: null);

        // Act
        var result = await sut.GetEpisodes(request, new IndexingContext());

        // Assert
        result.Results.Should().ContainSingle();
        result.Results![0].SpotifyId.Should().Be(free.Id);
    }

    private Mock<ISpotifyPodcastEpisodesProvider> CreateInnerProvider(
        IList<SimpleEpisode> episodes,
        bool expensive)
    {
        var inner = new Mock<ISpotifyPodcastEpisodesProvider>();
        inner
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new PodcastEpisodesResult(episodes, expensive));
        return inner;
    }

    private SimpleEpisode CreateSimpleEpisode(DateTime release, bool isPlayable = true)
    {
        var id = _fixture.CreateSpotifyId();
        return new SimpleEpisode
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            HtmlDescription = $"<p>{_fixture.CreateTitle()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Explicit = false,
            IsPlayable = isPlayable,
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(id).ToString()
            },
            Images = [],
            Type = ItemType.Episode
        };
    }

    private SpotifyEpisodeProvider CreateSut(ISpotifyPodcastEpisodesProvider inner) =>
        new(
            inner,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            new SpotifyEpisodeAdapter(),
            new EpisodeFromCandidateFactory(),
            NullLogger<SpotifyEpisodeProvider>.Instance);
}
