using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Adaptors;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Discovery submits (curator submit and supervisor apply) route each URL with the toolbar's channel-owner rule: a
/// channel / show a news organisation or TV show owns is stored under it and never becomes a new podcast. All ids and
/// names are synthetic.
/// </summary>
public class DiscoveryCatalogueParentRoutingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];
    private SubmitResult _parentResult = new(SubmitResultState.None, SubmitResultState.None);

    public DiscoveryCatalogueParentRoutingRules()
    {
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> p) => ToAsync(_organisations.Where(p.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> p) => ToAsync(_shows.Where(p.Compile())));
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(() => _parentResult);
        _mocker.Use<ISubmitResultAdaptor>(new SubmitResultAdaptor());
        _mocker.Use<ICatalogueParentLookup>(_mocker.CreateInstance<CatalogueParentLookup>());
        _mocker.Use<IEpisodeUrlRouter>(_mocker.CreateInstance<EpisodeUrlRouter>());
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private Uri SpotifyUrl() => new($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");

    private void OwnerIs(Uri url, Podcast? podcast, EpisodeUrlOwnerIds ids) =>
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.GetEpisodeUrlOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlOwner(podcast, ids));

    private NewsOrganisation AddOrganisation(Action<CatalogueIndexingSource> indexing)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Indexing = new CatalogueIndexingSource() };
        indexing(organisation.Indexing);
        _organisations.Add(organisation);
        return organisation;
    }

    private TvShow AddShow(Action<CatalogueIndexingSource> indexing)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Indexing = new CatalogueIndexingSource() };
        indexing(show.Indexing);
        _shows.Add(show);
        return show;
    }

    private static DiscoveryResult Result(Uri? youTube = null, Uri? spotify = null) =>
        new() { Urls = new DiscoveryResultUrls { YouTube = youTube, Spotify = spotify } };

    private static SubmitOptions Options() =>
        new(null, true, CreationSource: EpisodeCreationSource.Discovery);

    private void VerifySubmittedToParent(Guid parentId, Uri url, Times times) =>
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(
            s => s.Submit(It.Is<CatalogueParentMatch>(m => m.Parent.Id == parentId), url,
                It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()),
            times);

    private void VerifyNoPodcastPipeline() =>
        _mocker.GetMock<IDiscoveryResultProcessor>().Verify(
            p => p.CreateSubmitResult(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>(), It.IsAny<Podcast?>(), It.IsAny<Podcast?>(), It.IsAny<Podcast?>()),
            Times.Never);

    [Fact(DisplayName =
        "A discovered YouTube video whose channel no podcast owns, but a news organisation's indexing does, is stored as a news report under the organisation and never reaches the podcast pipeline, because a migrated outlet must not get a duplicate podcast.")]
    public async Task channel_owned_by_news_organisation_is_stored_under_it()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var playableId = _fixture.CreateGuid();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var searchSource = new CatalogueSearchSource(podcast, episode, CatalogueParentKind.NewsOrganisation);
        _parentResult = new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport, PlayableId: playableId, CatalogueSearchSource: searchSource);
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.Episode.Should().BeNull();
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.PlayableId.Should().Be(playableId);
        result.ParentId.Should().Be(organisation.Id);
        result.CatalogueSearchSource.Should().Be(searchSource);
        VerifySubmittedToParent(organisation.Id, url, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A discovered episode already stored under the TV show that owns its Spotify show reports EpisodeAlreadyExists with the TV episode id, so a re-submit is a no-op rather than a new podcast.")]
    public async Task existing_tv_episode_is_reported_as_already_existing()
    {
        // Arrange
        var spotifyShowId = _fixture.CreateSpotifyId();
        var show = AddShow(i => i.SpotifyId = spotifyShowId);
        var url = SpotifyUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(SpotifyShowId: spotifyShowId));
        var playableId = _fixture.CreateGuid();
        _parentResult = new SubmitResult(SubmitResultState.EpisodeAlreadyExists, SubmitResultState.None,
            ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId);
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(spotify: url), new IndexingContext(), Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EpisodeAlreadyExists);
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.PlayableId.Should().Be(playableId);
        result.ParentId.Should().Be(show.Id);
        result.CatalogueSearchSource.Should().BeNull();
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "When a discovery result's Spotify URL belongs to the TV show and its YouTube URL to a channel nobody owns, only the show's URL is filed under the show, because a URL from another channel must not be stored under a parent that does not own it (cross-service matching links it).")]
    public async Task unowned_extra_url_is_not_filed_under_the_parent()
    {
        // Arrange
        var spotifyShowId = _fixture.CreateSpotifyId();
        var show = AddShow(i => i.SpotifyId = spotifyShowId);
        var spotifyUrl = SpotifyUrl();
        var youTubeUrl = YouTubeUrl();
        OwnerIs(spotifyUrl, null, new EpisodeUrlOwnerIds(SpotifyShowId: spotifyShowId));
        OwnerIs(youTubeUrl, null, new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId()));
        _parentResult = new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
            ContentKind: SubmitClassification.TvShowEpisode, PlayableId: _fixture.CreateGuid());
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: youTubeUrl, spotify: spotifyUrl), new IndexingContext(),
            Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        VerifySubmittedToParent(show.Id, spotifyUrl, Times.Once());
        VerifySubmittedToParent(show.Id, youTubeUrl, Times.Never());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "When the TV show owns both the Spotify show and the YouTube channel of a discovery result, the second URL is submitted to the same show and its enrichment is reported, so both links land on one TV episode.")]
    public async Task second_owned_url_enriches_the_same_item()
    {
        // Arrange
        var spotifyShowId = _fixture.CreateSpotifyId();
        var channelId = _fixture.CreateYouTubeChannelId();
        var show = AddShow(i =>
        {
            i.SpotifyId = spotifyShowId;
            i.YouTubeChannelId = channelId;
        });
        var spotifyUrl = SpotifyUrl();
        var youTubeUrl = YouTubeUrl();
        OwnerIs(spotifyUrl, null, new EpisodeUrlOwnerIds(SpotifyShowId: spotifyShowId));
        OwnerIs(youTubeUrl, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var playableId = _fixture.CreateGuid();
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), spotifyUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.EpisodeAlreadyExists, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId));
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), youTubeUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Enriched, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: youTubeUrl, spotify: spotifyUrl), new IndexingContext(),
            Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.EnrichedEpisode);
        result.PlayableId.Should().Be(playableId);
        VerifySubmittedToParent(show.Id, spotifyUrl, Times.Once());
        VerifySubmittedToParent(show.Id, youTubeUrl, Times.Once());
    }

    [Fact(DisplayName =
        "When the first URL the TV show owns yields no item and the second URL it owns creates one, the result is CreatedEpisode with that item, because the stored item must not be reported as an error.")]
    public async Task item_created_by_second_owned_url_is_reported_created()
    {
        // Arrange
        var spotifyShowId = _fixture.CreateSpotifyId();
        var channelId = _fixture.CreateYouTubeChannelId();
        var show = AddShow(i =>
        {
            i.SpotifyId = spotifyShowId;
            i.YouTubeChannelId = channelId;
        });
        var spotifyUrl = SpotifyUrl();
        var youTubeUrl = YouTubeUrl();
        OwnerIs(spotifyUrl, null, new EpisodeUrlOwnerIds(SpotifyShowId: spotifyShowId));
        OwnerIs(youTubeUrl, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var playableId = _fixture.CreateGuid();
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), spotifyUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode));
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), youTubeUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: youTubeUrl, spotify: spotifyUrl), new IndexingContext(),
            Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.PlayableId.Should().Be(playableId);
        result.ParentId.Should().Be(show.Id);
    }

    [Fact(DisplayName =
        "Once the first owned URL has stored or matched the item, the other URLs the TV show owns are submitted enrich-only, and the first owned URL is not, because one discovered episode must never become two TV episodes.")]
    public async Task extra_owned_urls_are_enrich_only_once_the_item_exists()
    {
        // Arrange
        var spotifyShowId = _fixture.CreateSpotifyId();
        var channelId = _fixture.CreateYouTubeChannelId();
        var show = AddShow(i =>
        {
            i.SpotifyId = spotifyShowId;
            i.YouTubeChannelId = channelId;
        });
        var spotifyUrl = SpotifyUrl();
        var youTubeUrl = YouTubeUrl();
        OwnerIs(spotifyUrl, null, new EpisodeUrlOwnerIds(SpotifyShowId: spotifyShowId));
        OwnerIs(youTubeUrl, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var playableId = _fixture.CreateGuid();
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), spotifyUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode, PlayableId: playableId));
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), youTubeUrl, It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.None,
                ContentKind: SubmitClassification.TvShowEpisode));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: youTubeUrl, spotify: spotifyUrl), new IndexingContext(),
            Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.PlayableId.Should().Be(playableId);
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(
                It.Is<CatalogueParentMatch>(m => m.Parent.Id == show.Id), spotifyUrl, It.IsAny<IndexingContext>(),
                It.Is<SubmitOptions>(o => !o.EnrichOnly)),
            Times.Once);
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(
                It.Is<CatalogueParentMatch>(m => m.Parent.Id == show.Id), youTubeUrl, It.IsAny<IndexingContext>(),
                It.Is<SubmitOptions>(o => o.EnrichOnly)),
            Times.Once);
    }

    [Fact(DisplayName =
        "When no URL the news organisation owns produces an item, the discovery result fails with an error naming the organisation, because a bare 'unknown state' gives the curator nothing to act on.")]
    public async Task no_item_from_owned_urls_fails_with_clear_error()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        _parentResult = new SubmitResult(SubmitResultState.None, SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport);
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var act = () => sut.Submit(Result(youTube: url), new IndexingContext(), Options());

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should()
            .Contain(organisation.Name).And.Contain(organisation.Id.ToString());
    }

    [Fact(DisplayName =
        "When one URL of a discovery result belongs to a podcast and another to a news organisation, the result is DifferentPodcasts and nothing is stored, as for two different podcasts.")]
    public async Task podcast_and_parent_owners_are_different_owners()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        AddOrganisation(i => i.YouTubeChannelId = channelId);
        var youTubeUrl = YouTubeUrl();
        var spotifyUrl = SpotifyUrl();
        var podcast = _fixture.CreatePodcast();
        OwnerIs(youTubeUrl, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        OwnerIs(spotifyUrl, podcast, new EpisodeUrlOwnerIds(SpotifyShowId: _fixture.CreateSpotifyId()));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: youTubeUrl, spotify: spotifyUrl), new IndexingContext(),
            Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.DifferentPodcasts);
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(It.IsAny<CatalogueParentMatch>(),
            It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A news organisation that owns the channel but is removed fails the discovery item with a clear error and stores nothing, because a removed parent must not get new items and must not fall back to a podcast.")]
    public async Task removed_parent_fails_the_item()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        organisation.Removed = true;
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var act = () => sut.Submit(Result(youTube: url), new IndexingContext(), Options());

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*removed*");
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(It.IsAny<CatalogueParentMatch>(),
            It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A video on a news organisation's channel that is not in the playlist of the playlist-scoped podcast sharing that channel goes to the organisation, the same rule as the toolbar submit.")]
    public async Task playlist_scoped_podcast_yields_to_channel_owner()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        var url = YouTubeUrl();
        OwnerIs(url, podcast, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.IsVideoInPodcastPlaylist(It.Is<Podcast>(p => p.Id == podcast.Id), url,
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(false);
        _parentResult = new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport, PlayableId: _fixture.CreateGuid());
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), Options());

        // Assert
        result.ParentId.Should().Be(organisation.Id);
        VerifySubmittedToParent(organisation.Id, url, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "With no news organisation or TV show owning the channel, a discovered episode still goes through the podcast pipeline with its owning podcast, unchanged.")]
    public async Task podcast_owner_keeps_the_podcast_pipeline()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var url = YouTubeUrl();
        OwnerIs(url, podcast, new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId()));
        var episode = _fixture.CreateStoredEpisode(podcast);
        _mocker.GetMock<IDiscoveryResultProcessor>()
            .Setup(p => p.CreateSubmitResult(It.IsAny<DiscoveryResult>(), It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>(), It.IsAny<Podcast?>(), It.IsAny<Podcast?>(), It.IsAny<Podcast?>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.None, Episode: episode));
        var sut = _mocker.CreateInstance<DiscoveryUrlSubmitter>();

        // Act
        var result = await sut.Submit(Result(youTube: url), new IndexingContext(), Options());

        // Assert
        result.State.Should().Be(DiscoverySubmitResultState.CreatedEpisode);
        result.Episode.Should().Be(episode);
        result.ContentKind.Should().BeNull();
        _mocker.GetMock<IDiscoveryResultProcessor>().Verify(p => p.CreateSubmitResult(It.IsAny<DiscoveryResult>(),
            It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>(), null, null,
            It.Is<Podcast?>(x => x != null && x.Id == podcast.Id)), Times.Once);
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(It.IsAny<CatalogueParentMatch>(),
            It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
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
