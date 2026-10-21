using System.Linq.Expressions;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Discovery.Enrichers;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules.Enrichers;

/// <summary>
/// Discovery marks results whose channel / show belongs to a news organisation or TV show (its <c>indexing</c> ids) as
/// matched, and drops the ones the indexer already picks up. All ids and names are synthetic.
/// </summary>
public class EpisodeResultsEnricherCatalogueParentRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Podcast> _podcasts = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];

    public EpisodeResultsEnricherCatalogueParentRules()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> p) => ToAsync(_podcasts.Where(p.Compile())));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> p) => ToAsync(_organisations.Where(p.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> p) => ToAsync(_shows.Where(p.Compile())));
        _mocker.Use<IEpisodeResultEnricher>(new EpisodeResultEnricher());
    }

    private EpisodeResult CreateEpisodeResult(Action<PodcastServiceIds> ids)
    {
        var result = new EpisodeResult(
            _fixture.Create<string>(),
            DateTime.UtcNow.AddDays(-1),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            null,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DiscoverService.YouTube);
        ids(result.PodcastIds);
        return result;
    }

    private NewsOrganisation AddOrganisation(Action<CatalogueIndexingSource> indexing)
    {
        var organisation = new NewsOrganisation(_fixture.Create<string>()) { Indexing = new CatalogueIndexingSource() };
        indexing(organisation.Indexing);
        _organisations.Add(organisation);
        return organisation;
    }

    private TvShow AddShow(Action<CatalogueIndexingSource> indexing)
    {
        var show = new TvShow(_fixture.Create<string>()) { Indexing = new CatalogueIndexingSource() };
        indexing(show.Indexing);
        _shows.Add(show);
        return show;
    }

    private async Task<List<RedditPodcastPoster.Discovery.Models.EnrichedEpisodeResult>> Enrich(EpisodeResult result)
    {
        var sut = _mocker.CreateInstance<EpisodeResultsEnricher>();
        return await sut.EnrichWithPodcastDetails([result]).ToListAsync();
    }

    [Fact(DisplayName =
        "A discovered video on a YouTube channel that a news organisation indexes all episodes of is matched to the organisation's id as indexing everything, so discovery drops it (the indexer already ingests it) instead of offering it as an unknown channel.")]
    public async Task auto_indexed_news_organisation_channel_is_matched_and_indexed()
    {
        // Arrange
        var channelId = _fixture.Create<string>();
        var organisation = AddOrganisation(i =>
        {
            i.YouTubeChannelId = channelId;
            i.IndexAllEpisodes = true;
        });
        var result = CreateEpisodeResult(ids => ids.YouTube = channelId);

        // Act
        var enriched = await Enrich(result);

        // Assert
        var match = enriched.Single().PodcastResults.Should().ContainSingle().Subject;
        match.PodcastId.Should().Be(organisation.Id);
        match.IndexAllEpisodes.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A discovered episode of a Spotify show a TV show keeps but does not auto-index is matched to the show's id and not marked as indexed, so the curator sees it as a known show rather than an unknown one.")]
    public async Task tv_show_spotify_show_is_matched_but_not_indexed()
    {
        // Arrange
        var spotifyId = _fixture.Create<string>();
        var show = AddShow(i => i.SpotifyId = spotifyId);
        var result = CreateEpisodeResult(ids => ids.Spotify = spotifyId);

        // Act
        var enriched = await Enrich(result);

        // Assert
        var match = enriched.Single().PodcastResults.Should().ContainSingle().Subject;
        match.PodcastId.Should().Be(show.Id);
        match.IndexAllEpisodes.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When two TV shows keep the same Apple podcast id, the auto-indexed one is the match, the same tie-break as podcasts sharing an id.")]
    public async Task shared_apple_id_prefers_auto_indexed_parent()
    {
        // Arrange
        var appleId = _fixture.Create<long>();
        AddShow(i => i.AppleId = appleId);
        var indexed = AddShow(i =>
        {
            i.AppleId = appleId;
            i.IndexAllEpisodes = true;
        });
        var result = CreateEpisodeResult(ids => ids.Apple = appleId);

        // Act
        var enriched = await Enrich(result);

        // Assert
        enriched.Single().PodcastResults.Should().ContainSingle().Which.PodcastId.Should().Be(indexed.Id);
    }

    [Fact(DisplayName =
        "A podcast and a news organisation both on the episode's channel are both matched, so neither owner is hidden from the curator.")]
    public async Task podcast_and_parent_are_both_matched()
    {
        // Arrange
        var channelId = _fixture.Create<string>();
        var podcast = new Podcast { Id = _fixture.Create<Guid>(), Name = _fixture.Create<string>(), YouTubeChannelId = channelId };
        _podcasts.Add(podcast);
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var result = CreateEpisodeResult(ids => ids.YouTube = channelId);

        // Act
        var enriched = await Enrich(result);

        // Assert
        enriched.Single().PodcastResults.Select(x => x.PodcastId).Should()
            .BeEquivalentTo([podcast.Id, organisation.Id]);
    }

    [Fact(DisplayName =
        "A discovered episode whose channel no podcast, news organisation or TV show owns has no match.")]
    public async Task unowned_channel_has_no_match()
    {
        // Arrange
        AddOrganisation(i => i.YouTubeChannelId = _fixture.Create<string>());
        var result = CreateEpisodeResult(ids => ids.YouTube = _fixture.Create<string>());

        // Act
        var enriched = await Enrich(result);

        // Assert
        enriched.Single().PodcastResults.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "News organisations and TV shows are read once per discovery run, not once per result, because a run enriches hundreds of results and these containers are small.")]
    public async Task parents_are_loaded_once_per_run()
    {
        // Arrange
        var channelId = _fixture.Create<string>();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        EpisodeResult[] results =
        [
            CreateEpisodeResult(ids => ids.YouTube = channelId),
            CreateEpisodeResult(ids => ids.Spotify = _fixture.Create<string>())
        ];
        var sut = _mocker.CreateInstance<EpisodeResultsEnricher>();

        // Act
        var enriched = await sut.EnrichWithPodcastDetails(results).ToListAsync();

        // Assert
        enriched[0].PodcastResults.Should().ContainSingle().Which.PodcastId.Should().Be(organisation.Id);
        enriched[1].PodcastResults.Should().BeEmpty();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()), Times.Once);
        _mocker.GetMock<ITvShowRepository>().Verify(
            r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()), Times.Once);
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
