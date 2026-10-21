using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// A URL from a channel / show owned by a news organisation or TV show (its <c>indexing</c> ids) is stored
/// under that parent, never as a new podcast. All ids and names are synthetic.
/// </summary>
public class CatalogueParentRoutingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<NewsReport> _reports = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<NewsReport> _savedReports = [];
    private IReadOnlyList<Podcast> _podcasts = [];

    public CatalogueParentRoutingRules()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> p) => ToAsync(_podcasts.Where(p.Compile())));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> p) => ToAsync(_organisations.Where(p.Compile())));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(x => x.Id == id));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> p) => ToAsync(_shows.Where(p.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.SingleOrDefault(x => x.Id == id));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> p) => _reports.FirstOrDefault(p.Compile()));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => { _reports.Remove(report); _reports.Add(report); _savedReports.Add(report); })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> p) => _tvEpisodes.FirstOrDefault(p.Compile()));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => { _tvEpisodes.Remove(episode); _tvEpisodes.Add(episode); })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns((Guid id) => ToAsync(_reports.Where(x => x.NewsOrganisationId == id)));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns((Guid id) => ToAsync(_tvEpisodes.Where(x => x.TvShowId == id)));
        _mocker.Use<ICatalogueParentLookup>(_mocker.CreateInstance<CatalogueParentLookup>());
        _mocker.Use<IEpisodeUrlRouter>(_mocker.CreateInstance<EpisodeUrlRouter>());
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private void OwnerIs(Uri url, Podcast? podcast, EpisodeUrlOwnerIds ids) =>
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.GetEpisodeUrlOwner(url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlOwner(podcast, ids));

    private NewsOrganisation AddOrganisation(Action<CatalogueIndexingSource>? indexing = null, string? name = null)
    {
        var organisation = new NewsOrganisation(name ?? _fixture.CreateTitle());
        if (indexing != null)
        {
            organisation.Indexing = new CatalogueIndexingSource();
            indexing(organisation.Indexing);
        }

        _organisations.Add(organisation);
        return organisation;
    }

    private TvShow AddShow(Action<CatalogueIndexingSource>? indexing = null, string? name = null)
    {
        var show = new TvShow(name ?? _fixture.CreateTitle());
        if (indexing != null)
        {
            show.Indexing = new CatalogueIndexingSource();
            indexing(show.Indexing);
        }

        _shows.Add(show);
        return show;
    }

    private void VerifyRoutedTo(Guid parentId, Times times) =>
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(
            s => s.Submit(
                It.Is<CatalogueParentMatch>(m => m.Parent.Id == parentId),
                It.IsAny<Uri>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<SubmitOptions>()),
            times);

    private void VerifyNoPodcastPipeline() =>
        _mocker.GetMock<ICategorisedItemProcessor>().Verify(
            p => p.ProcessCategorisedItem(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()),
            Times.Never);

    [Fact(DisplayName =
        "A YouTube URL whose channel no podcast owns, but a news organisation's indexing does, is stored under the organisation and never reaches the podcast pipeline.")]
    public async Task channel_owned_by_news_organisation_routes_there()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var expected = new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
            ContentKind: SubmitClassification.NewsReport, PlayableId: Guid.NewGuid());
        _mocker.GetMock<ICatalogueParentSubmitter>()
            .Setup(s => s.Submit(It.IsAny<CatalogueParentMatch>(), url, It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(expected);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.Should().Be(expected);
        VerifyRoutedTo(organisation.Id, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A news organisation with no indexing block, or an indexing block with no YouTube channel id, gets a URL whose channel title is its name (any case) and nothing goes to the podcast pipeline.")]
    public async Task unclaimed_parent_named_like_channel_title_gets_url()
    {
        // Arrange
        var name = "Synthetic Outlet " + _fixture.CreateTitle();
        var organisation = AddOrganisation(name: name);
        var otherName = "Synthetic Drama " + _fixture.CreateTitle();
        var show = AddShow(i => i.SpotifyId = _fixture.CreateSpotifyId(), otherName);
        var organisationUrl = YouTubeUrl();
        var showUrl = YouTubeUrl();
        OwnerIs(organisationUrl, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(),
                YouTubeChannelTitle: name.ToUpperInvariant()));
        OwnerIs(showUrl, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(),
                YouTubeChannelTitle: " " + otherName + " "));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(organisationUrl, new IndexingContext(), new SubmitOptions(null, true));
        await sut.Submit(showUrl, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Once());
        VerifyRoutedTo(show.Id, Times.Once());
        VerifyNoPodcastPipeline();
        organisation.Indexing.Should().BeNull("the fallback only routes; the curator sets the channel id");
    }

    [Fact(DisplayName =
        "A parent named like the channel title but claiming a different YouTube channel is skipped: the URL goes to the podcast pipeline.")]
    public async Task parent_claiming_another_channel_is_skipped_by_title()
    {
        // Arrange
        var name = "Synthetic Outlet " + _fixture.CreateTitle();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = _fixture.CreateYouTubeChannelId(), name);
        var url = YouTubeUrl();
        OwnerIs(url, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(), YouTubeChannelTitle: name));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Never());
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                null, url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Two unclaimed parents named like the channel title are ambiguous (a parent claiming another channel is left out), and nothing is stored.")]
    public async Task many_unclaimed_parents_by_title_are_ambiguous()
    {
        // Arrange
        var name = "Synthetic Shared " + _fixture.CreateTitle();
        var organisation = AddOrganisation(name: name);
        var show = AddShow(i => i.YouTubeChannelId = string.Empty, name.ToLowerInvariant());
        AddShow(i => i.YouTubeChannelId = _fixture.CreateYouTubeChannelId(), name);
        var url = YouTubeUrl();
        OwnerIs(url, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(), YouTubeChannelTitle: name));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var act = () => sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        var ex = await act.Should().ThrowAsync<AmbiguousParentNameException>();
        ex.Which.ParentIds.Should().BeEquivalentTo([organisation.Id, show.Id]);
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A TV show whose indexing has an empty-string YouTube channel id counts as unclaimed and gets a URL whose channel title is its name.")]
    public async Task parent_with_empty_channel_id_gets_url_by_title()
    {
        // Arrange
        var name = "Synthetic Drama " + _fixture.CreateTitle();
        var show = AddShow(i => i.YouTubeChannelId = string.Empty, name);
        var url = YouTubeUrl();
        OwnerIs(url, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(), YouTubeChannelTitle: name));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(show.Id, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A removed parent named like the channel title is not matched by title: the active namesake gets the URL, and with none the URL goes to the podcast pipeline.")]
    public async Task removed_parent_is_skipped_by_title()
    {
        // Arrange
        var name = "Synthetic Outlet " + _fixture.CreateTitle();
        var removed = AddOrganisation(name: name);
        removed.Removed = true;
        var active = AddShow(name: name);
        var activeUrl = YouTubeUrl();
        OwnerIs(activeUrl, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(), YouTubeChannelTitle: name));
        var lonelyName = "Synthetic Gone " + _fixture.CreateTitle();
        var lonelyRemoved = AddOrganisation(name: lonelyName);
        lonelyRemoved.Removed = true;
        var lonelyUrl = YouTubeUrl();
        OwnerIs(lonelyUrl, null,
            new EpisodeUrlOwnerIds(YouTubeChannelId: _fixture.CreateYouTubeChannelId(),
                YouTubeChannelTitle: lonelyName));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(activeUrl, new IndexingContext(), new SubmitOptions(null, true));
        var lonelyResult = await sut.Submit(lonelyUrl, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(active.Id, Times.Once());
        VerifyRoutedTo(removed.Id, Times.Never());
        VerifyRoutedTo(lonelyRemoved.Id, Times.Never());
        lonelyResult.PodcastResult.Should().NotBe(SubmitResultState.PodcastRemoved);
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                null, lonelyUrl, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A podcast that owns the channel still gets the URL even when an unclaimed parent is named like the channel title.")]
    public async Task podcast_owner_wins_over_title_fallback()
    {
        // Arrange
        var name = "Synthetic Outlet " + _fixture.CreateTitle();
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(name: name);
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, podcast, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId, YouTubeChannelTitle: name));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Never());
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == podcast.Id), url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When a podcast owns the channel it still gets the URL, even if a news organisation has the same channel id.")]
    public async Task podcast_owner_wins_over_parent()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, podcast, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Never());
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == podcast.Id), url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "With create-podcast set, a channel a TV show owns is still stored under the show: no podcast is created for it.")]
    public async Task create_podcast_never_creates_for_owned_channel()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var show = AddShow(i => i.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true, CreatePodcast: true));

        // Assert
        VerifyRoutedTo(show.Id, Times.Once());
        VerifyNoPodcastPipeline();
        _mocker.GetMock<IPodcastAndEpisodeFactory>().Verify(
            f => f.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName =
        "A name that no podcast has but a TV show has (any case) targets the show.")]
    public async Task name_targets_tv_show_when_no_podcast_has_it()
    {
        // Arrange
        var name = "Synthetic Drama " + _fixture.CreateTitle();
        var show = AddShow(name: name);
        var url = YouTubeUrl();
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true, PodcastName: name.ToUpperInvariant()));

        // Assert
        VerifyRoutedTo(show.Id, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A name shared by a news organisation and a TV show is a 409-style ambiguity carrying both ids.")]
    public async Task name_shared_across_kinds_is_ambiguous()
    {
        // Arrange
        var name = "Synthetic Shared " + _fixture.CreateTitle();
        var organisation = AddOrganisation(name: name);
        var show = AddShow(name: name);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var act = () => sut.Submit(YouTubeUrl(), new IndexingContext(), new SubmitOptions(null, true, PodcastName: name));

        // Assert
        var ex = await act.Should().ThrowAsync<AmbiguousParentNameException>();
        ex.Which.ParentIds.Should().BeEquivalentTo([organisation.Id, show.Id]);
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A name no podcast or parent has, on a channel a news organisation owns, goes to the organisation instead of creating a podcast with that name.")]
    public async Task unknown_name_on_owned_channel_routes_by_channel()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true, PodcastName: _fixture.CreateTitle()));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Once());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "An id that is not a podcast but is a news organisation targets it; an id that is neither is still podcast-not-found.")]
    public async Task id_targets_parent_or_not_found()
    {
        // Arrange
        var organisation = AddOrganisation();
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(YouTubeUrl(), new IndexingContext(), new SubmitOptions(organisation.Id, true));
        var missing = () => sut.Submit(YouTubeUrl(), new IndexingContext(), new SubmitOptions(Guid.NewGuid(), true));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Once());
        await missing.Should().ThrowAsync<SubmitPodcastNotFoundException>();
    }

    [Fact(DisplayName =
        "A removed owning parent returns podcast-removed and stores nothing.")]
    public async Task removed_parent_stores_nothing()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        organisation.Removed = true;
        var url = YouTubeUrl();
        OwnerIs(url, null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var result = await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        result.PodcastResult.Should().Be(SubmitResultState.PodcastRemoved);
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        VerifyRoutedTo(organisation.Id, Times.Never());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "A non-podcast-platform URL (BBC, other sites) is never routed by parent name: the existing catalogue-kind flow handles it.")]
    public async Task non_platform_url_is_not_routed()
    {
        // Arrange
        var name = "Synthetic Outlet " + _fixture.CreateTitle();
        AddOrganisation(name: name);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(new Uri($"https://example.com/{_fixture.CreateYouTubeId()}"), new IndexingContext(),
            new SubmitOptions(null, true, PodcastName: name));

        // Assert
        _mocker.GetMock<ICatalogueParentSubmitter>().Verify(s => s.Submit(It.IsAny<CatalogueParentMatch>(),
            It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
    }

    [Fact(DisplayName =
        "Owner lookup matches Spotify show ids and Apple podcast ids in indexing, and two channel-wide parents on one channel are ambiguous unless one is auto-indexed or the other is playlist-scoped.")]
    public async Task owner_lookup_rules()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        var channelId = _fixture.CreateYouTubeChannelId();
        var spotifyShow = AddShow(i => i.SpotifyId = spotifyId);
        var appleOrganisation = AddOrganisation(i => i.AppleId = appleId);
        var channelWide = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var playlistScoped = AddShow(i =>
        {
            i.YouTubeChannelId = channelId;
            i.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        var sut = _mocker.Get<ICatalogueParentLookup>();

        // Act
        var bySpotify = await sut.FindByOwner(new EpisodeUrlOwnerIds(SpotifyShowId: spotifyId));
        var byApple = await sut.FindByOwner(new EpisodeUrlOwnerIds(ApplePodcastId: appleId));
        var byChannel = await sut.FindByOwner(new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        var none = await sut.FindByOwner(EpisodeUrlOwnerIds.None);
        var secondChannelWide = AddShow(i => i.YouTubeChannelId = channelId);
        var ambiguous = () => sut.FindByOwner(new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));

        // Assert
        bySpotify!.Parent.Id.Should().Be(spotifyShow.Id);
        bySpotify.Kind.Should().Be(CatalogueParentKind.TvShow);
        byApple!.Parent.Id.Should().Be(appleOrganisation.Id);
        byChannel!.Parent.Id.Should().Be(channelWide.Id);
        playlistScoped.Id.Should().NotBe(byChannel.Parent.Id);
        none.Should().BeNull();
        (await ambiguous.Should().ThrowAsync<AmbiguousParentNameException>())
            .Which.ParentIds.Should().BeEquivalentTo([channelWide.Id, secondChannelWide.Id]);
        secondChannelWide.Indexing!.IndexAllEpisodes = true;
        (await sut.FindByOwner(new EpisodeUrlOwnerIds(YouTubeChannelId: channelId)))!.Parent.Id
            .Should().Be(secondChannelWide.Id);
    }

    /// <summary>
    /// Modelled on a news outlet's channel that also hosts one playlist-scoped podcast (a show of the outlet,
    /// not auto-indexed). Synthetic ids only.
    /// </summary>
    private (Podcast Show, NewsOrganisation Outlet, Uri Url) ArrangeSharedChannel()
    {
        var channelId = _fixture.CreateYouTubeChannelId();
        var outlet = AddOrganisation(i => i.YouTubeChannelId = channelId, name: "Synthetic News Network");
        var show = _fixture.CreatePodcast(p =>
        {
            p.Name = "Synthetic Scoop";
            p.PublisherName = "Synthetic News Network";
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks;
            p.IndexAllEpisodes = false;
        });
        _podcasts = [show];
        var url = YouTubeUrl();
        OwnerIs(url, show, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId));
        return (show, outlet, url);
    }

    private void InPlaylist(Podcast podcast, Uri url, bool? inPlaylist) =>
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.IsVideoInPodcastPlaylist(
                It.Is<Podcast>(p => p.Id == podcast.Id), url, It.IsAny<IndexingContext>()))
            .ReturnsAsync(inPlaylist);

    [Fact(DisplayName =
        "Shared channel: a video that is not in the playlist-scoped podcast's playlist goes to the news organisation that owns the channel, not the podcast.")]
    public async Task shared_channel_video_outside_playlist_goes_to_outlet()
    {
        // Arrange
        var (show, outlet, url) = ArrangeSharedChannel();
        InPlaylist(show, url, false);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(outlet.Id, Times.Once());
        VerifyNoPodcastPipeline();
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == show.Id), It.IsAny<Uri>(), It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Shared channel: a video in the podcast's playlist stays with the podcast.")]
    public async Task shared_channel_video_in_playlist_stays_with_podcast()
    {
        // Arrange
        var (show, outlet, url) = ArrangeSharedChannel();
        InPlaylist(show, url, true);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        VerifyRoutedTo(outlet.Id, Times.Never());
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == show.Id), url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Shared channel: when playlist membership cannot be checked the submit fails without storing anything or creating a podcast.")]
    public async Task shared_channel_unknown_membership_fails_safely()
    {
        // Arrange
        var (show, outlet, url) = ArrangeSharedChannel();
        InPlaylist(show, url, null);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var act = () => sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyRoutedTo(outlet.Id, Times.Never());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "Shared channel: explicitly naming the playlist-scoped podcast still wins, without a playlist check.")]
    public async Task shared_channel_explicit_podcast_name_wins()
    {
        // Arrange
        var (show, outlet, url) = ArrangeSharedChannel();
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true, PodcastName: show.Name));

        // Assert
        VerifyRoutedTo(outlet.Id, Times.Never());
        _mocker.GetMock<IPodcastService>().Verify(s => s.IsVideoInPodcastPlaylist(
            It.IsAny<Podcast>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == show.Id), url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A playlist-scoped podcast on a channel no news organisation or TV show owns keeps the URL as before, without a playlist check.")]
    public async Task playlist_scoped_podcast_without_parent_unchanged()
    {
        // Arrange
        var (show, outlet, url) = ArrangeSharedChannel();
        _organisations.Remove(outlet);
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        _mocker.GetMock<IPodcastService>().Verify(s => s.IsVideoInPodcastPlaylist(
            It.IsAny<Podcast>(), It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast?>(p => p != null && p.Id == show.Id), url, It.IsAny<IndexingContext>(),
                It.IsAny<bool>(), It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    private (CatalogueParentSubmitter Sut, Episode Episode, Uri Url) ArrangeSubmitter(Uri? submitted = null)
    {
        var url = submitted ?? YouTubeUrl();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = new() { Url = url } };
        });
        ProcessorReturns(new SubmitResult(SubmitResultState.Created, SubmitResultState.None, Episode: episode));
        return (_mocker.CreateInstance<CatalogueParentSubmitter>(), episode, url);
    }

    private void ProcessorReturns(SubmitResult result) =>
        _mocker.GetMock<IPodcastProcessor>()
            .Setup(p => p.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(result);

    private NewsReport StoreReport(NewsOrganisation organisation, Uri youTubeUrl)
    {
        var stored = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration(),
            Guests = [_fixture.CreateTitle(2)],
            Services = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = new() { Url = youTubeUrl } }
        };
        stored.SetNewsOrganisationProperties(organisation);
        _reports.Add(stored);
        return stored;
    }

    [Fact(DisplayName =
        "The parent submitter runs the URL as the parent's transient podcast, saves a NewsReport with the episode's id under the organisation, never saves a podcast or episode, and hands back what to index.")]
    public async Task parent_submitter_saves_news_report()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var organisation = AddOrganisation(i => i.YouTubeChannelId = channelId);
        var (sut, episode, url) = ArrangeSubmitter();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.Is<Podcast>(p => p.Id == organisation.Id && p.YouTubeChannelId == channelId &&
                                    p.Name == organisation.Name),
                It.Is<IReadOnlyList<Episode>>(known => known.Count == 0),
                url, It.IsAny<IndexingContext>(), true, It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
        _reports.Should().ContainSingle();
        var report = _reports[0];
        report.Id.Should().Be(episode.Id);
        report.NewsOrganisationId.Should().Be(organisation.Id);
        report.Services![ServiceKeys.YouTube].Url.Should().Be(url);
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.PlayableId.Should().Be(episode.Id);
        result.Episode.Should().BeNull();
        result.CatalogueSearchSource!.Kind.Should().Be(CatalogueParentKind.NewsOrganisation);
        result.CatalogueSearchSource.Parent.Id.Should().Be(organisation.Id);
        result.CatalogueSearchSource.Episode.Id.Should().Be(episode.Id);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "The parent submitter saves a TvShowEpisode for a TV show, even one without an indexing block.")]
    public async Task parent_submitter_saves_tv_episode()
    {
        // Arrange
        var show = AddShow();
        var (sut, episode, url) = ArrangeSubmitter();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(show), url, new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        _tvEpisodes.Should().ContainSingle().Which.TvShowId.Should().Be(show.Id);
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.CatalogueSearchSource!.Kind.Should().Be(CatalogueParentKind.TvShow);
        show.Indexing.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the parent already has a playable with that URL, nothing is saved or indexed and its id is returned; with persist off nothing is saved either.")]
    public async Task parent_submitter_existing_or_dry_run()
    {
        // Arrange
        var organisation = AddOrganisation();
        var (sut, _, url) = ArrangeSubmitter();
        var stored = StoreReport(organisation, url);
        var other = AddOrganisation();

        // Act
        var existing = await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(),
            new SubmitOptions(null, true));
        var dryRun = await sut.Submit(CatalogueParentMatch.For(other), url, new IndexingContext(),
            new SubmitOptions(null, true, PersistToDatabase: false));

        // Assert
        existing.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        existing.PlayableId.Should().Be(stored.Id);
        existing.CatalogueSearchSource.Should().BeNull();
        dryRun.EpisodeResult.Should().Be(SubmitResultState.Created);
        dryRun.CatalogueSearchSource.Should().BeNull();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "With enrich-only set, a URL that matches no stored playable creates nothing and returns no item, because discovery's extra URLs may only enrich the item the first URL stored, never add a second one.")]
    public async Task parent_submitter_enrich_only_never_creates()
    {
        // Arrange
        var organisation = AddOrganisation();
        var (sut, _, url) = ArrangeSubmitter();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(),
            new SubmitOptions(null, true, EnrichOnly: true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.None);
        result.PlayableId.Should().BeNull();
        result.CatalogueSearchSource.Should().BeNull();
        _reports.Should().BeEmpty();
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.Save(It.IsAny<NewsReport>()), Times.Never);
    }

    [Fact(DisplayName =
        "With enrich-only set, a URL the processor matches to a stored report still enriches and saves that report and returns its id, because enrich-only blocks only creation, not adding links to the discovered item.")]
    public async Task parent_submitter_enrich_only_still_enriches_matched_report()
    {
        // Arrange
        var organisation = AddOrganisation();
        var stored = StoreReport(organisation, YouTubeUrl());
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");
        var merged = CatalogueParentKnownEpisodes.ToEpisode(stored, new Podcast { Id = organisation.Id });
        merged.Services![ServiceKeys.Spotify] = new ServiceLink { Url = spotifyUrl };
        ProcessorReturns(new SubmitResult(SubmitResultState.Enriched, SubmitResultState.None, Episode: merged));
        var sut = _mocker.CreateInstance<CatalogueParentSubmitter>();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), spotifyUrl, new IndexingContext(),
            new SubmitOptions(null, true, EnrichOnly: true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.PlayableId.Should().Be(stored.Id);
        result.CatalogueSearchSource!.Episode.Id.Should().Be(stored.Id);
        _reports.Should().ContainSingle();
        _savedReports.Should().ContainSingle().Which.Should().BeSameAs(stored);
        stored.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
    }

    [Fact(DisplayName =
        "The parent submitter matches the URL against the parent's stored playables (projected with ids from their service urls), like a podcast's episodes, so the categoriser and processor can find an existing item.")]
    public async Task parent_submitter_passes_stored_playables_as_known_episodes()
    {
        // Arrange
        var organisation = AddOrganisation();
        var videoId = _fixture.CreateYouTubeId();
        var stored = StoreReport(organisation, new Uri($"https://www.youtube.com/watch?v={videoId}"));
        AddOrganisation();
        var (sut, _, url) = ArrangeSubmitter();

        // Act
        await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(), new SubmitOptions(null, true));

        // Assert
        _mocker.GetMock<IUrlCategoriser>().Verify(c => c.Categorise(
                It.IsAny<Podcast>(),
                It.Is<IReadOnlyList<Episode>>(known => known.Count == 1 && known[0].Id == stored.Id &&
                                                       known[0].Ids!.YouTube == videoId &&
                                                       known[0].Guests!.SequenceEqual(stored.Guests!)),
                url, It.IsAny<IndexingContext>(), true, It.IsAny<NonPodcastServiceItemMetaData?>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "A cross-platform resubmit the processor matches to a stored report and enriches copies the new service and subjects onto that report, saves it, re-indexes it and returns Enriched with its id, never a second report.")]
    public async Task parent_submitter_enriches_matched_report()
    {
        // Arrange
        var organisation = AddOrganisation();
        var stored = StoreReport(organisation, YouTubeUrl());
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}");
        var subject = _fixture.CreateTitle(1);
        var merged = CatalogueParentKnownEpisodes.ToEpisode(stored, new Podcast { Id = organisation.Id });
        merged.Services![ServiceKeys.Spotify] = new ServiceLink { Url = spotifyUrl };
        merged.Subjects = [subject];
        ProcessorReturns(new SubmitResult(SubmitResultState.Enriched, SubmitResultState.None, Episode: merged));
        var sut = _mocker.CreateInstance<CatalogueParentSubmitter>();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), spotifyUrl, new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.PlayableId.Should().Be(stored.Id);
        result.CatalogueSearchSource!.Episode.Id.Should().Be(stored.Id);
        _reports.Should().ContainSingle();
        _savedReports.Should().ContainSingle().Which.Should().BeSameAs(stored);
        stored.Services![ServiceKeys.Spotify].Url.Should().Be(spotifyUrl);
        stored.Services.Should().ContainKey(ServiceKeys.YouTube);
        stored.Subjects.Should().Equal(subject);
    }

    [Fact(DisplayName =
        "A resubmit the processor matches to a stored report with nothing new is EpisodeAlreadyExists with that report's id, and nothing is saved or indexed.")]
    public async Task parent_submitter_known_item_already_exists()
    {
        // Arrange
        var organisation = AddOrganisation();
        var url = YouTubeUrl();
        var stored = StoreReport(organisation, url);
        var matched = CatalogueParentKnownEpisodes.ToEpisode(stored, new Podcast { Id = organisation.Id });
        ProcessorReturns(new SubmitResult(SubmitResultState.EpisodeAlreadyExists, SubmitResultState.None, Episode: matched));
        var sut = _mocker.CreateInstance<CatalogueParentSubmitter>();

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.PlayableId.Should().Be(stored.Id);
        result.CatalogueSearchSource.Should().BeNull();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The same video under another URL form (youtu.be vs watch?v=) is EpisodeAlreadyExists even when the processor would create it, because the stored report's YouTube id matches.")]
    public async Task parent_submitter_same_video_other_url_form()
    {
        // Arrange
        var organisation = AddOrganisation();
        var videoId = _fixture.CreateYouTubeId();
        var stored = StoreReport(organisation, new Uri($"https://www.youtube.com/watch?v={videoId}"));
        var (sut, _, url) = ArrangeSubmitter(new Uri($"https://youtu.be/{videoId}"));

        // Act
        var result = await sut.Submit(CatalogueParentMatch.For(organisation), url, new IndexingContext(),
            new SubmitOptions(null, true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.PlayableId.Should().Be(stored.Id);
        _savedReports.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "An explicit parent target files the URL under that news organisation by id with no podcast lookup or owner routing, and never creates a podcast.")]
    public async Task explicit_parent_target_routes_by_id()
    {
        // Arrange
        var organisation = AddOrganisation();
        var url = YouTubeUrl();
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        await sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true,
            Parent: new CatalogueParentTarget(CatalogueParentKind.NewsOrganisation, organisation.Id, organisation.Name)));

        // Assert
        VerifyRoutedTo(organisation.Id, Times.Once());
        VerifyNoPodcastPipeline();
        _mocker.GetMock<IPodcastService>().Verify(
            s => s.GetEpisodeUrlOwner(It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.GetPodcast(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "An explicit parent target whose id is not a row of that kind (missing, or a TV show sent as a news organisation) is parent-not-found and nothing is stored.")]
    public async Task explicit_parent_target_missing_is_not_found()
    {
        // Arrange
        var show = AddShow();
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var wrongKind = () => sut.Submit(YouTubeUrl(), new IndexingContext(), new SubmitOptions(null, true,
            Parent: new CatalogueParentTarget(CatalogueParentKind.NewsOrganisation, show.Id)));
        var missing = () => sut.Submit(YouTubeUrl(), new IndexingContext(), new SubmitOptions(null, true,
            Parent: new CatalogueParentTarget(CatalogueParentKind.TvShow, _fixture.CreateGuid())));

        // Assert
        (await wrongKind.Should().ThrowAsync<SubmitParentNotFoundException>()).Which.ParentId.Should().Be(show.Id);
        await missing.Should().ThrowAsync<SubmitParentNotFoundException>();
        VerifyRoutedTo(show.Id, Times.Never());
        VerifyNoPodcastPipeline();
    }

    [Fact(DisplayName =
        "When the channel / show owner of a URL with no podcast match cannot be resolved, the submit fails closed instead of creating a podcast, because a news organisation or TV show might own the channel.")]
    public async Task owner_lookup_failure_fails_closed()
    {
        // Arrange
        var url = YouTubeUrl();
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.GetEpisodeUrlOwner(url, It.IsAny<IndexingContext>()))
            .ThrowsAsync(new HttpRequestException("synthetic quota failure"));
        var sut = _mocker.CreateInstance<UrlSubmitter>();

        // Act
        var create = () => sut.Submit(url, new IndexingContext(), new SubmitOptions(null, true, CreatePodcast: true));
        var byUnknownName = () => sut.Submit(url, new IndexingContext(),
            new SubmitOptions(null, true, PodcastName: _fixture.CreateTitle()));

        // Assert
        await create.Should().ThrowAsync<InvalidOperationException>();
        await byUnknownName.Should().ThrowAsync<InvalidOperationException>();
        VerifyNoPodcastPipeline();
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
