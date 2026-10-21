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
            .Callback<NewsReport>(_reports.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> p) => _tvEpisodes.FirstOrDefault(p.Compile()));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(_tvEpisodes.Add)
            .Returns(Task.CompletedTask);
        _mocker.Use<ICatalogueParentLookup>(_mocker.CreateInstance<CatalogueParentLookup>());
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

    private (CatalogueParentSubmitter Sut, Episode Episode, Uri Url) ArrangeSubmitter()
    {
        var url = YouTubeUrl();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Services = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = new() { Url = url } };
        });
        _mocker.GetMock<IPodcastProcessor>()
            .Setup(p => p.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.None, Episode: episode));
        return (_mocker.CreateInstance<CatalogueParentSubmitter>(), episode, url);
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
                It.Is<Podcast?>(p => p != null && p.Id == organisation.Id && p.YouTubeChannelId == channelId &&
                                     p.Name == organisation.Name),
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
        var stored = new NewsReport(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink> { [ServiceKeys.YouTube] = new() { Url = url } }
        };
        stored.SetNewsOrganisationProperties(organisation);
        _reports.Add(stored);
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

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}
