using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Tubi.Matching;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Tests.Support;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueKindSubmitRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Film> _films = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShowEpisode> _episodes = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<NewsReport> _reports = [];

    public CatalogueKindSubmitRules()
    {
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(x => x.ResolveTvShowEpisodeAsync(
                It.IsAny<TvShow>(),
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) =>
                _films.FirstOrDefault(predicate.Compile()));
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.Save(It.IsAny<Film>()))
            .Callback<Film>(_films.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(Empty<TvShow>());
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(_shows.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> predicate) =>
                _episodes.FirstOrDefault(predicate.Compile()));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(_episodes.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(Empty<NewsOrganisation>());
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(_organisations.Add)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> predicate) =>
                _reports.FirstOrDefault(predicate.Compile()));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(_reports.Add)
            .Returns(Task.CompletedTask);
        TakenFileKeys<IFilmRepository, Film>(_films);
        TakenFileKeys<ITvShowRepository, TvShow>(_shows);
        TakenFileKeys<INewsOrganisationRepository, NewsOrganisation>(_organisations);
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
    }

    [Fact(DisplayName =
        "A news submit whose outlet name is Korean-only creates the news organisation with a key built from its id (news-id_…), never news- or news-_.")]
    public async Task non_latin_news_organisation_gets_id_key()
    {
        // Arrange
        UseEnabledFlag();
        var source = Source(showName: "오늘의 뉴스");
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        var organisation = _organisations.Should().ContainSingle().Subject;
        organisation.FileKey.Should().Be(
            FileKeyFactory.NewsOrganisationPrefix + FileKeyFactory.GetIdSlug(organisation.Id));
    }

    [Fact(DisplayName =
        "A series submit whose show name is Korean-only creates the TV show with a key built from its id (tvshow-id_…).")]
    public async Task non_latin_tv_show_gets_id_key()
    {
        // Arrange
        UseEnabledFlag();
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(Source(showName: "주간 이야기")), TvOptions());

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        var show = _shows.Should().ContainSingle().Subject;
        show.FileKey.Should().Be(FileKeyFactory.TvShowPrefix + FileKeyFactory.GetIdSlug(show.Id));
    }

    [Fact(DisplayName =
        "A film submit whose title is emoji-only creates the film with a key built from its id (film-id_…).")]
    public async Task emoji_only_film_gets_id_key()
    {
        // Arrange
        UseEnabledFlag();
        var source = Source() with { Title = "🎙️📻 ✨" };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), FilmOptions());

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        var film = _films.Should().ContainSingle().Subject;
        film.FileKey.Should().Be(FileKeyFactory.FilmPrefix + FileKeyFactory.GetIdSlug(film.Id));
    }

    private void TakenFileKeys<TRepository, TEntity>(List<TEntity> store)
        where TRepository : class, IFilterableRepository<TEntity> =>
        _mocker.GetMock<TRepository>()
            .Setup(x => x.GetAllBy(
                It.IsAny<Expression<Func<TEntity, bool>>>(),
                It.IsAny<Expression<Func<TEntity, string>>>()))
            .Returns((Expression<Func<TEntity, bool>> selector, Expression<Func<TEntity, string>> projection) =>
                Yield(store.Where(selector.Compile()).Select(projection.Compile())));

    [Fact(DisplayName =
        "When the submit content-type flag is off, a made-as-film signal does not write a Film " +
        "and stays on the podcast episode path.")]
    public async Task flag_off_does_not_write_a_film()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(), FilmOptions());

        // Assert
        result.Should().BeNull();
        _films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on and the item is a standalone film, " +
        "submit writes a Film with no parent and does not return to the podcast path.")]
    public async Task flag_on_writes_a_film_without_a_parent()
    {
        // Arrange
        UseEnabledFlag();
        var poster = new Uri($"https://example.com/{_fixture.CreateYouTubeId()}");
        var source = Source() with { Image = poster };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), FilmOptions(source.Url!));

        // Assert
        result.Should().NotBeNull();
        result!.Episode.Should().BeNull();
        result.Podcast.Should().BeNull();
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.PlayableId.Should().Be(_films[0].Id);
        _films.Should().ContainSingle();
        _films[0].Name.Should().Be(source.Title);
        _films[0].Services.Should().ContainKey("netflix");
        _films[0].Services!["netflix"].Url.Should().Be(source.Url);
        _films[0].Services!["netflix"].Image.Should().Be(poster);
        _episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on and no TvShow has that series name, " +
        "submit creates the TvShow and a TvShowEpisode and does not write a podcast episode.")]
    public async Task flag_on_creates_a_tv_show_and_episode()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var source = Source(showName: series);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        _shows.Should().ContainSingle();
        _shows[0].Name.Should().Be(series);
        _episodes.Should().ContainSingle();
        _episodes[0].TvShowId.Should().Be(_shows[0].Id);
        _episodes[0].TvShowName.Should().Be(series);
        _episodes[0].Title.Should().Be(source.Title);
        _films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When more than one TvShow already has the series name, submit refuses to attach " +
        "and does not write a TvShow episode, because the curator must choose the parent.")]
    public async Task duplicate_series_name_is_refused()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var existing = new[] { new TvShow(series), new TvShow(series) };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(Yield(existing));
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var act = () => sut.TrySubmit(Categorised(Source(showName: series)), TvOptions());

        // Assert
        await act.Should().ThrowAsync<AmbiguousParentNameException>();
        _episodes.Should().BeEmpty();
        _shows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on and the signal is news, " +
        "submit creates a NewsOrganisation and a NewsReport and does not write a Film.")]
    public async Task flag_on_creates_a_news_organisation_and_report()
    {
        // Arrange
        UseEnabledFlag();
        var organisation = _fixture.CreateTitle();
        var source = Source(showName: organisation);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        _organisations.Should().ContainSingle();
        _organisations[0].Name.Should().Be(organisation);
        _reports.Should().ContainSingle();
        _reports[0].NewsOrganisationId.Should().Be(_organisations[0].Id);
        _reports[0].NewsOrganisationName.Should().Be(organisation);
        _films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a news URL submit creates a NewsOrganisation, it is saved with bypass-short-episodes and " +
        "always-promote-as-hero on and no service ids, because those are the saved news defaults and a curator may " +
        "switch them off later.")]
    public async Task news_create_saves_the_news_default_flags()
    {
        // Arrange
        UseEnabledFlag();
        var source = Source(showName: _fixture.CreateTitle());
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        var indexing = _organisations.Should().ContainSingle().Subject.Indexing!;
        indexing.BypassShortEpisodeChecking.Should().BeTrue();
        indexing.AlwaysPromoteAsHero.Should().BeTrue();
        indexing.IsAutoIndexed.Should().BeFalse();
        indexing.YouTubeChannelId.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a news URL is submitted to an existing NewsOrganisation whose curator switched both flags off, the " +
        "report is added and the organisation is not saved or changed, because the news defaults apply only at " +
        "creation.")]
    public async Task report_into_existing_news_organisation_keeps_its_flags()
    {
        // Arrange
        UseEnabledFlag();
        var outlet = _fixture.CreateTitle();
        var existing = new NewsOrganisation(outlet)
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { BypassShortEpisodeChecking = false, AlwaysPromoteAsHero = false }
        };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> predicate) =>
                Yield(new[] { existing }.Where(predicate.Compile())));
        var source = OutletSource(TubiMovieUrl(), outlet);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        _reports.Should().ContainSingle().Which.NewsOrganisationId.Should().Be(existing.Id);
        _organisations.Should().BeEmpty();
        existing.Indexing.BypassShortEpisodeChecking.Should().BeFalse();
        existing.Indexing.AlwaysPromoteAsHero.Should().BeFalse();
    }

    [Theory(DisplayName =
        "When a news URL submit creates a NewsOrganisation, the new report is appended to the hero list only when it " +
        "was released inside the week window, because the new organisation is saved with always-promote-as-hero on and " +
        "the classifier path must promote like the podcast submit does.")]
    [InlineData(1, true)]
    [InlineData(20, false)]
    public async Task news_create_promotes_hero_when_eligible(int daysAgo, bool expectPromoted)
    {
        // Arrange
        UseEnabledFlag();
        var source = Source(showName: _fixture.CreateTitle()) with { Release = DomainTestFixture.UtcDaysAgo(daysAgo) };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        _organisations.Should().ContainSingle();
        var report = _reports.Should().ContainSingle().Subject;
        VerifyHeroPromoted(report.Id, expectPromoted);
    }

    [Theory(DisplayName =
        "When a news URL is submitted to an existing NewsOrganisation, the new report from this week is appended to the " +
        "hero list only when the organisation is flagged always-promote-as-hero, because the flag must behave as it does " +
        "for a podcast submit.")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task report_into_existing_news_organisation_promotes_hero_only_when_flagged(
        bool? alwaysPromote, bool expectPromoted)
    {
        // Arrange
        UseEnabledFlag();
        var outlet = _fixture.CreateTitle();
        var existing = new NewsOrganisation(outlet)
        {
            Id = _fixture.CreateGuid(),
            Indexing = new CatalogueIndexingSource { AlwaysPromoteAsHero = alwaysPromote }
        };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> predicate) =>
                Yield(new[] { existing }.Where(predicate.Compile())));
        var source = OutletSource(TubiMovieUrl(), outlet) with { Release = DomainTestFixture.UtcDaysAgo(1) };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        var report = _reports.Should().ContainSingle().Subject;
        VerifyHeroPromoted(report.Id, expectPromoted);
    }

    [Theory(DisplayName =
        "When a TV URL is submitted to an existing TvShow, the new episode from this week is appended to the hero list " +
        "only when the show is flagged always-promote-as-hero, and a newly created show (no flag) is not promoted, " +
        "because TV shows have no hero default.")]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(null, true, false)]
    public async Task tv_submit_promotes_hero_only_when_show_flagged(
        bool? alwaysPromote, bool createShow, bool expectPromoted)
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        if (!createShow)
        {
            UseStoredShows(new TvShow(series)
            {
                Id = _fixture.CreateGuid(),
                Indexing = new CatalogueIndexingSource { AlwaysPromoteAsHero = alwaysPromote }
            });
        }

        var source = Source(showName: series) with { Release = DomainTestFixture.UtcDaysAgo(1) };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), TvOptions(source.Url));

        // Assert
        var episode = _episodes.Should().ContainSingle().Subject;
        _shows.Should().HaveCount(createShow ? 1 : 0);
        VerifyHeroPromoted(episode.Id, expectPromoted);
    }

    [Fact(DisplayName =
        "The classifier submit measures the hero week window with the injected TimeProvider, so a report released a " +
        "day before that clock is promoted even though it is weeks old by the wall clock.")]
    public async Task news_create_hero_window_uses_time_provider()
    {
        // Arrange
        UseEnabledFlag();
        _mocker.Use<TimeProvider>(new FixedTimeProvider(DomainTestFixture.UtcDaysAgo(30)));
        var source = Source(showName: _fixture.CreateTitle()) with { Release = DomainTestFixture.UtcDaysAgo(31) };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        var report = _reports.Should().ContainSingle().Subject;
        VerifyHeroPromoted(report.Id, expectPromoted: true);
    }

    [Fact(DisplayName =
        "A dry-run news submit creates nothing and promotes no hero, because nothing was stored.")]
    public async Task news_dry_run_does_not_promote_hero()
    {
        // Arrange
        UseEnabledFlag();
        var source = Source(showName: _fixture.CreateTitle()) with { Release = DomainTestFixture.UtcDaysAgo(1) };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!) with { PersistToDatabase = false });

        // Assert
        _reports.Should().BeEmpty();
        _mocker.GetMock<IHeroEpisodePromoter>().Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the same film URL is submitted again, including a different paste that canonicalizes " +
        "to the stored service link, submit returns that Film and does not insert another.")]
    public async Task second_film_url_does_not_insert_another_film()
    {
        // Arrange
        UseEnabledFlag();
        var (firstUrl, secondUrl, canonical) = TwoUrlsForSameFilm();
        var source = FilmSource(firstUrl);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var first = await sut.TrySubmit(Categorised(source), FilmOptions(firstUrl));
        var second = await sut.TrySubmit(Categorised(FilmSource(secondUrl, source.Title)), FilmOptions(secondUrl));

        // Assert
        first!.EpisodeResult.Should().Be(SubmitResultState.Created);
        first.PlayableId.Should().Be(_films[0].Id);
        _films.Should().ContainSingle();
        _films[0].Services![StreamingServiceWire.ToKey(StreamingService.Tubi)].Url.Should().Be(canonical);
        canonical.Should().NotBe(firstUrl);
        canonical.Should().NotBe(secondUrl);
        second!.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        second.ContentKind.Should().Be(SubmitClassification.Film);
        second.PlayableId.Should().Be(_films[0].Id);
        _films.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the same TvShow episode URL is submitted again, submit returns that episode " +
        "and does not insert another TvShow episode.")]
    public async Task second_tv_episode_url_does_not_insert_another_episode()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var (firstUrl, secondUrl, _) = TwoUrlsForSameSeries();
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var first = await sut.TrySubmit(Categorised(SeriesSource(firstUrl, series)), TvOptions(firstUrl));
        var second = await sut.TrySubmit(Categorised(SeriesSource(secondUrl, series)), TvOptions(secondUrl));

        // Assert
        first!.PlayableId.Should().Be(_episodes[0].Id);
        second!.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        second.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        second.PlayableId.Should().Be(_episodes[0].Id);
        _episodes.Should().ContainSingle();
        _shows.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the same news URL is submitted again, submit returns that NewsReport " +
        "and does not insert another report.")]
    public async Task second_news_url_does_not_insert_another_report()
    {
        // Arrange
        UseEnabledFlag();
        var outlet = _fixture.CreateTitle();
        var (firstUrl, secondUrl, _) = TwoUrlsForSameFilm();
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var first = await sut.TrySubmit(Categorised(OutletSource(firstUrl, outlet)), NewsOptions(firstUrl));
        var second = await sut.TrySubmit(Categorised(OutletSource(secondUrl, outlet)), NewsOptions(secondUrl));

        // Assert
        first!.PlayableId.Should().Be(_reports[0].Id);
        second!.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        second.ContentKind.Should().Be(SubmitClassification.NewsReport);
        second.PlayableId.Should().Be(_reports[0].Id);
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When one TvShow already has the series name, submit reuses that show " +
        "and does not save the TvShow again.")]
    public async Task one_existing_tv_show_is_reused()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var existing = new TvShow(series);
        UseStoredShows(existing);
        var source = SeriesSource(TubiMovieUrl(), series);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.PodcastResult.Should().Be(SubmitResultState.None);
        _shows.Should().BeEmpty();
        _episodes.Should().ContainSingle();
        _episodes[0].TvShowId.Should().Be(existing.Id);
        _episodes[0].TvShowName.Should().Be(existing.Name);
    }

    [Fact(DisplayName =
        "When one TvShow matches the series name ignoring case, submit reuses that show " +
        "and does not create a second parent.")]
    public async Task tv_show_name_match_ignores_case()
    {
        // Arrange
        UseEnabledFlag();
        var storedName = _fixture.CreateTitle();
        var submittedName = DifferentCasing(storedName);
        var existing = new TvShow(storedName);
        UseStoredShows(existing);
        var source = SeriesSource(TubiMovieUrl(), submittedName);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        submittedName.Should().NotBe(storedName);
        result!.PodcastResult.Should().Be(SubmitResultState.None);
        _shows.Should().BeEmpty();
        _episodes.Should().ContainSingle();
        _episodes[0].TvShowId.Should().Be(existing.Id);
    }

    [Fact(DisplayName =
        "When several TvShows share a series name apart from case, submit refuses to attach " +
        "and does not write a TvShow episode.")]
    public async Task case_variants_of_one_series_name_are_ambiguous()
    {
        // Arrange
        UseEnabledFlag();
        var basis = _fixture.CreateTitle().ToLowerInvariant();
        var mixed = MixCasing(basis);
        UseStoredShows(new TvShow(basis), new TvShow(basis.ToUpperInvariant()));
        var source = SeriesSource(TubiMovieUrl(), mixed);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var act = () => sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        var ex = await act.Should().ThrowAsync<AmbiguousParentNameException>();
        ex.Which.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        ex.Which.ParentName.Should().Be(mixed);
        ex.Which.ParentIds.Should().HaveCount(2);
        _episodes.Should().BeEmpty();
        _shows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an exact series name has a case-variant sibling stored, submit refuses to attach " +
        "and does not write a TvShow episode, because both parents still match.")]
    public async Task exact_series_name_with_a_case_variant_sibling_is_ambiguous()
    {
        // Arrange
        UseEnabledFlag();
        var exactName = _fixture.CreateTitle();
        var siblingName = DifferentCasing(exactName);
        var exact = new TvShow(exactName);
        var sibling = new TvShow(siblingName);
        UseStoredShows(exact, sibling);
        var source = SeriesSource(TubiMovieUrl(), exactName);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var act = () => sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        siblingName.Should().NotBe(exactName);
        var ex = await act.Should().ThrowAsync<AmbiguousParentNameException>();
        ex.Which.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        ex.Which.ParentName.Should().Be(exactName);
        ex.Which.ParentIds.Should().BeEquivalentTo([exact.Id, sibling.Id]);
        _episodes.Should().BeEmpty();
        _shows.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a TV submit has a platform publisher but no series name, submit does not create " +
        "a TvShow named after that publisher.")]
    public async Task tv_submit_without_a_series_name_does_not_use_the_publisher()
    {
        // Arrange
        UseEnabledFlag();
        var publisher = _fixture.Create<string>();
        var source = FilmSource(TubiMovieUrl()) with { Publisher = publisher, ShowName = null };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var act = () => sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _shows.Should().BeEmpty();
        _episodes.Should().BeEmpty();
        _shows.Should().NotContain(show => show.Name == publisher);
    }

    [Fact(DisplayName =
        "When a news submit has a scraped outlet name and no series name, submit stores that outlet " +
        "as the NewsOrganisation.")]
    public async Task news_submit_uses_the_scraped_outlet_name()
    {
        // Arrange
        UseEnabledFlag();
        var outlet = _fixture.Create<string>();
        var source = FilmSource(TubiMovieUrl()) with { Publisher = outlet, ShowName = null };
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        result!.ContentKind.Should().Be(SubmitClassification.NewsReport);
        _organisations.Should().ContainSingle();
        _organisations[0].Name.Should().Be(outlet);
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on, a Netflix catalogue /title/ URL is a reject " +
        "and writes neither a Film nor a TvShow episode.")]
    public async Task netflix_title_hub_is_a_reject_and_writes_nothing()
    {
        // Arrange
        UseEnabledFlag();
        var url = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        var item = new CategorisedItem(null, null, null, null, null, null, null, Service.Other);
        var signals = SubmitContentClassifier.FromSubmission(url, item);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(
            item,
            new SubmitOptions(null, MatchOtherServices: false, PersistToDatabase: true, ClassificationSignals: signals));

        // Assert
        result.Should().NotBeNull();
        result!.Rejected.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.PlayableId.Should().BeNull();
        _films.Should().BeEmpty();
        _episodes.Should().BeEmpty();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on, an unconfirmed Vimeo URL requires a curator " +
        "and writes no playable, so the client can tell a hold from an empty success.")]
    public async Task unconfirmed_vimeo_requires_a_curator_and_writes_nothing()
    {
        // Arrange
        UseEnabledFlag();
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var item = new CategorisedItem(null, null, null, null, null, null, null, Service.Other);
        var signals = SubmitContentClassifier.FromSubmission(url, item);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(
            item,
            new SubmitOptions(null, MatchOtherServices: false, PersistToDatabase: true, ClassificationSignals: signals));

        // Assert
        result.Should().NotBeNull();
        result!.RequiresCurator.Should().BeTrue();
        result.Rejected.Should().BeFalse();
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        _films.Should().BeEmpty();
        _episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is on, a BBC /news/ URL is a NewsReport hint, " +
        "writes no podcast playable, and does not throw.")]
    public async Task bbc_news_url_is_unpersisted_and_does_not_throw()
    {
        // Arrange
        UseEnabledFlag();
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}");
        var item = new CategorisedItem(null, null, null, null, null, null, null, Service.Other);
        var signals = SubmitContentClassifier.FromSubmission(url, item);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(
            item,
            new SubmitOptions(null, MatchOtherServices: false, PersistToDatabase: true, ClassificationSignals: signals));

        // Assert
        result.Should().NotBeNull();
        result!.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.Rejected.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
        result.Episode.Should().BeNull();
        result.Podcast.Should().BeNull();
        result.PlayableId.Should().BeNull();
        _films.Should().BeEmpty();
        _reports.Should().BeEmpty();
        _episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an iPlayer episode URL has a scraped series name and no matching podcast, " +
        "submit writes a TvShow and TvShowEpisode, because iPlayer is television catalogue.")]
    public async Task iplayer_series_without_a_matching_podcast_writes_a_tv_show()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var source = new ResolvedNonPodcastServiceItem(
            StreamingService.BbcIplayer,
            Url: url,
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            ShowName: series);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(url));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        _shows.Should().ContainSingle();
        _shows[0].Name.Should().Be(series);
        _episodes.Should().ContainSingle();
        _episodes[0].TvShowId.Should().Be(_shows[0].Id);
        _films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an iPlayer submit scrapes a network publisher and creates a new TvShow, " +
        "the show stores that publisher so the curator Meta field is not left blank.")]
    public async Task iplayer_create_sets_tv_show_publisher_from_scrape()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var source = new ResolvedNonPodcastServiceItem(
            StreamingService.BbcIplayer,
            Url: url,
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            Publisher: publisher,
            ShowName: series);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(url));

        // Assert
        result!.EpisodeResult.Should().Be(SubmitResultState.Created);
        _shows.Should().ContainSingle();
        _shows[0].Name.Should().Be(series);
        _shows[0].PublisherName.Should().Be(publisher);
        _shows[0].ImdbId.Should().BeNull();
        _shows[0].TvdbId.Should().BeNull();
        _episodes.Should().ContainSingle();
        _episodes[0].ImdbId.Should().BeNull();
        _episodes[0].TvdbId.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a kind-classified TV submit creates a TvShowEpisode, the result carries a " +
        "CatalogueSearchSource keyed by the playable id so the host indexes it immediately.")]
    public async Task tv_create_returns_catalogue_search_source_for_immediate_index()
    {
        // Arrange
        UseEnabledFlag();
        var series = _fixture.CreateTitle();
        var source = Source(showName: series);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), TvOptions(source.Url!));

        // Assert
        result!.CatalogueSearchSource.Should().NotBeNull();
        result.CatalogueSearchSource!.Kind.Should().Be(CatalogueParentKind.TvShow);
        result.CatalogueSearchSource.Parent.Id.Should().Be(_shows[0].Id);
        result.CatalogueSearchSource.Parent.Name.Should().Be(series);
        result.CatalogueSearchSource.Episode.Id.Should().Be(_episodes[0].Id);
        result.CatalogueSearchSource.Episode.Title.Should().Be(source.Title);
        result.PlayableId.Should().Be(_episodes[0].Id);
    }

    [Fact(DisplayName =
        "When a kind-classified news submit creates a NewsReport, the result carries a " +
        "CatalogueSearchSource keyed by the playable id so the host indexes it immediately.")]
    public async Task news_create_returns_catalogue_search_source_for_immediate_index()
    {
        // Arrange
        UseEnabledFlag();
        var organisation = _fixture.CreateTitle();
        var source = Source(showName: organisation);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(Categorised(source), NewsOptions(source.Url!));

        // Assert
        result!.CatalogueSearchSource.Should().NotBeNull();
        result.CatalogueSearchSource!.Kind.Should().Be(CatalogueParentKind.NewsOrganisation);
        result.CatalogueSearchSource.Parent.Id.Should().Be(_organisations[0].Id);
        result.CatalogueSearchSource.Episode.Id.Should().Be(_reports[0].Id);
        result.PlayableId.Should().Be(_reports[0].Id);
    }

    [Fact(DisplayName =
        "When submit already resolved a matching podcast, a film URL still writes a Film, " +
        "because a unique podcast name must not swallow a standalone film.")]
    public async Task matching_podcast_still_writes_a_film()
    {
        // Arrange
        UseEnabledFlag();
        var podcast = _fixture.CreatePodcast();
        var source = FilmSource(TubiMovieUrl());
        var item = new CategorisedItem(podcast, null, null, null, null, null, source, Service.Other);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(item, FilmOptions(source.Url));

        // Assert
        result!.ContentKind.Should().Be(SubmitClassification.Film);
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        _films.Should().ContainSingle();
        _films[0].Name.Should().Be(source.Title);
        _shows.Should().BeEmpty();
        _episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When submit already resolved a matching podcast, a news URL still writes a NewsReport, " +
        "because a unique podcast name must not swallow news.")]
    public async Task matching_podcast_still_writes_a_news_report()
    {
        // Arrange
        UseEnabledFlag();
        var podcast = _fixture.CreatePodcast();
        var outlet = _fixture.CreateTitle();
        var source = OutletSource(TubiMovieUrl(), outlet);
        var item = new CategorisedItem(podcast, null, null, null, null, null, source, Service.Other);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(item, NewsOptions(source.Url!));

        // Assert
        result!.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        _organisations.Should().ContainSingle();
        _reports.Should().ContainSingle();
        _films.Should().BeEmpty();
        _shows.Should().BeEmpty();
        _episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When submit already resolved a matching podcast, a BBC Sounds play URL with a series name " +
        "returns to the podcast path and writes no TvShow, because Sounds is audio catalogue.")]
    public async Task sounds_with_a_matching_podcast_does_not_mint_a_tv_show()
    {
        // Arrange
        UseEnabledFlag();
        var podcast = _fixture.CreatePodcast();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");
        var source = new ResolvedNonPodcastServiceItem(
            StreamingService.BbcSounds,
            Url: url,
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            ShowName: _fixture.CreateTitle());
        var item = new CategorisedItem(podcast, null, null, null, null, null, source, Service.Other);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var result = await sut.TrySubmit(
            item,
            new SubmitOptions(
                null,
                MatchOtherServices: false,
                PersistToDatabase: true,
                ClassificationSignals: SubmitContentClassifier.FromSubmission(url, item)));

        // Assert
        result.Should().BeNull();
        _shows.Should().BeEmpty();
        _episodes.Should().BeEmpty();
        _films.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a podcast Episode already stores the canonical service URL, a film or series submit " +
        "with the content-type flag on returns that Episode and writes no Film, TvShow, or NewsReport.")]
    public async Task stored_episode_blocks_a_catalogue_sibling()
    {
        // Arrange
        UseEnabledFlag();
        var podcast = _fixture.CreatePodcast();
        var (submitted, _, canonical) = TwoUrlsForSameFilm();
        var seriesName = _fixture.CreateTitle();
        var outlet = _fixture.Create<string>();
        var episode = _fixture.CreateStoredEpisode(podcast, stored =>
        {
            stored.Services = new Dictionary<string, ServiceLink>
            {
                [StreamingServiceWire.ToKey(StreamingService.Tubi)] = new() { Url = canonical }
            };
        });
        var filmSource = FilmSource(submitted);
        var seriesSource = SeriesSource(submitted, seriesName);
        var newsSource = OutletSource(submitted, outlet);
        var sut = _mocker.CreateInstance<CatalogueKindSubmitter>();

        // Act
        var film = await sut.TrySubmit(WithMatchingEpisode(episode, filmSource), FilmOptions(submitted));
        var series = await sut.TrySubmit(WithMatchingEpisode(episode, seriesSource), TvOptions(submitted));
        var news = await sut.TrySubmit(WithMatchingEpisode(episode, newsSource), NewsOptions(submitted));

        // Assert
        film!.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        film.ContentKind.Should().Be(SubmitClassification.Episode);
        film.PlayableId.Should().Be(episode.Id);
        series!.ContentKind.Should().Be(SubmitClassification.Episode);
        series.PlayableId.Should().Be(episode.Id);
        series.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        news!.ContentKind.Should().Be(SubmitClassification.Episode);
        news.PlayableId.Should().Be(episode.Id);
        news.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        _films.Should().BeEmpty();
        _shows.Should().BeEmpty();
        _episodes.Should().BeEmpty();
        _organisations.Should().BeEmpty();
        _reports.Should().BeEmpty();
        episode.Services![StreamingServiceWire.ToKey(StreamingService.Tubi)].Url.Should().Be(canonical);
    }

    private void VerifyHeroPromoted(Guid playableId, bool expectPromoted) =>
        _mocker.GetMock<IHeroEpisodePromoter>().Verify(
            x => x.PromoteAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { playableId })),
                It.IsAny<CancellationToken>()),
            expectPromoted ? Times.Once() : Times.Never());

    private void UseEnabledFlag() =>
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));

    private SubmitOptions FilmOptions(Uri? url = null) =>
        new(
            null,
            MatchOtherServices: false,
            PersistToDatabase: true,
            ClassificationSignals: new SubmitClassificationSignals(
                url ?? new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true));

    private SubmitOptions NewsOptions(Uri url) =>
        new(
            null,
            MatchOtherServices: false,
            PersistToDatabase: true,
            ClassificationSignals: new SubmitClassificationSignals(url, News: true));

    private SubmitOptions TvOptions(Uri? url = null) =>
        new(
            null,
            MatchOtherServices: false,
            PersistToDatabase: true,
            ClassificationSignals: new SubmitClassificationSignals(
                url ?? new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Series: true));

    private CategorisedItem Categorised(ResolvedNonPodcastServiceItem? source = null) =>
        new(null, null, null, null, null, null, source ?? Source(), Service.Other);

    private static CategorisedItem WithMatchingEpisode(Episode episode, ResolvedNonPodcastServiceItem source) =>
        new(null, null, episode, null, null, null, source, Service.Other);

    private ResolvedNonPodcastServiceItem Source(string? showName = null) =>
        new(
            StreamingService.Netflix,
            Url: new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            ShowName: showName);

    private ResolvedNonPodcastServiceItem FilmSource(Uri url, string? title = null) =>
        new(
            StreamingService.Tubi,
            Url: url,
            Title: title ?? _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            MadeAsFilm: true);

    private ResolvedNonPodcastServiceItem SeriesSource(Uri url, string showName) =>
        FilmSource(url) with { ShowName = showName, MadeAsFilm = false };

    private ResolvedNonPodcastServiceItem OutletSource(Uri url, string outlet) =>
        FilmSource(url) with { Publisher = outlet, ShowName = outlet, MadeAsFilm = false };

    private (Uri First, Uri Second, Uri Canonical) TwoUrlsForSameFilm()
    {
        var id = _fixture.CreateAppleId();
        var first = new Uri($"https://www.tubitv.com/en-au/movies/{id}/{_fixture.CreateYouTubeId()}");
        var second = new Uri($"https://tubitv.com/en-gb/movies/{id}/{_fixture.CreateYouTubeId()}");
        var canonical = TubiUrlMatcher.CanonicalUrl(first);
        TubiUrlMatcher.CanonicalUrl(second).Should().Be(canonical);
        return (first, second, canonical);
    }

    private (Uri First, Uri Second, Uri Canonical) TwoUrlsForSameSeries()
    {
        var id = _fixture.CreateAppleId();
        var first = new Uri($"https://www.tubitv.com/en-au/tv-shows/{id}/{_fixture.CreateYouTubeId()}");
        var second = new Uri($"https://tubitv.com/en-gb/tv-shows/{id}/{_fixture.CreateYouTubeId()}");
        var canonical = TubiUrlMatcher.CanonicalUrl(first);
        TubiUrlMatcher.CanonicalUrl(second).Should().Be(canonical);
        return (first, second, canonical);
    }

    private Uri TubiMovieUrl()
    {
        var id = _fixture.CreateAppleId();
        return new Uri($"https://tubitv.com/movies/{id}");
    }

    private void UseStoredShows(params TvShow[] shows)
    {
        var stored = shows.ToList();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> predicate) => Yield(stored.Where(predicate.Compile())));
    }

    private static string DifferentCasing(string value)
    {
        var upper = value.ToUpperInvariant();
        return string.Equals(upper, value, StringComparison.Ordinal)
            ? value.ToLowerInvariant()
            : upper;
    }

    private static string MixCasing(string lower)
    {
        var mixed = new string(lower.Select((character, index) =>
            index % 2 == 0 ? char.ToUpperInvariant(character) : character).ToArray());
        return mixed;
    }

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<T> Yield<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
