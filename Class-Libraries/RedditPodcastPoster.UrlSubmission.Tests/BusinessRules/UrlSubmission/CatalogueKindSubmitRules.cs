using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Tubi.Matching;
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
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create());
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
    }

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
        var source = Source();
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
