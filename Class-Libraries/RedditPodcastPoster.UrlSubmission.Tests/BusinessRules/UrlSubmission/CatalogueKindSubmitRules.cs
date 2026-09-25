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
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
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
