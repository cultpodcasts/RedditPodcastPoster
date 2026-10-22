using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class HomepageCataloguePlayablesRules
{
    private static readonly DateTime Since = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<INewsReportRepository> _newsReports = new();
    private readonly Mock<INewsOrganisationRepository> _newsOrganisations = new();
    private readonly Mock<ITvShowEpisodeRepository> _tvShowEpisodes = new();
    private readonly Mock<ITvShowRepository> _tvShows = new();
    private readonly Mock<IFilmRepository> _films = new();

    public HomepageCataloguePlayablesRules()
    {
        GivenNewsReports();
        GivenTvShowEpisodes();
        GivenFilms();
    }

    private HomepageCataloguePlayables Sut() => new(
        _newsReports.Object,
        _newsOrganisations.Object,
        _tvShowEpisodes.Object,
        _tvShows.Object,
        _films.Object,
        NullLogger<HomepageCataloguePlayables>.Instance);

    private void GivenNewsReports(params NewsReport[] reports) =>
        _newsReports
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(() => reports.ToAsyncEnumerable());

    private void GivenTvShowEpisodes(params TvShowEpisode[] episodes) =>
        _tvShowEpisodes
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(() => episodes.ToAsyncEnumerable());

    private void GivenFilms(params Film[] films) =>
        _films
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(() => films.ToAsyncEnumerable());

    private NewsOrganisation GivenOrganisation(string name, bool removed = false, CatalogueIndexingSource? indexing = null)
    {
        var organisation = new NewsOrganisation(name) { Removed = removed, Indexing = indexing, KnownTerms = ["SyntheticTerm"] };
        _newsOrganisations.Setup(x => x.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        return organisation;
    }

    private TvShow GivenShow(string name, bool removed = false)
    {
        var show = new TvShow(name) { Removed = removed };
        _tvShows.Setup(x => x.GetTvShow(show.Id)).ReturnsAsync(show);
        return show;
    }

    private static NewsReport Report(NewsOrganisation organisation, string title, DateTime release)
    {
        var report = new NewsReport(title)
        {
            NewsOrganisationId = organisation.Id,
            NewsOrganisationName = organisation.Name,
            Description = title + " description",
            Length = TimeSpan.FromMinutes(4),
            Subjects = ["Synthetic Subject"]
        };
        report.SetRelease(CatalogueRelease.FromDateTimeUtc(release));
        return report;
    }

    private static TvShowEpisode TvEpisode(TvShow show, string title, DateTime release)
    {
        var episode = new TvShowEpisode(title)
        {
            TvShowId = show.Id,
            TvShowName = show.Name,
            Length = TimeSpan.FromMinutes(45)
        };
        episode.SetRelease(CatalogueRelease.FromDate(DateOnly.FromDateTime(release)));
        return episode;
    }

    private static Film MakeFilm(string name, CatalogueRelease release)
    {
        var film = new Film(name) { Length = TimeSpan.FromMinutes(90) };
        film.SetRelease(release);
        return film;
    }

    [Fact(DisplayName =
        "Homepage catalogue: a recent news report comes back as a NewsReport carrying its GUID, the news " +
        "organisation's current name, and the organisation's title/description regexes and known terms, " +
        "because the parent stands in for the podcast when the homepage sanitises it.")]
    public async Task news_report_maps_with_parent_name_and_regexes()
    {
        // Arrange
        var organisation = GivenOrganisation("Synthetic News Desk",
            indexing: new CatalogueIndexingSource { TitleRegex = "^Synthetic: ", DescriptionRegex = "^(?<body>.*)$" });
        var report = Report(organisation, "Synthetic report", Since.AddDays(2));
        report.NewsOrganisationName = "Stale Name";
        GivenNewsReports(report);

        // Act
        var result = await Sut().GetRecent(Since, CancellationToken.None);

        // Assert
        var item = result.Should().ContainSingle().Subject;
        item.ContentKind.Should().Be("NewsReport");
        item.Id.Should().Be(report.Id);
        item.ParentName.Should().Be("Synthetic News Desk");
        item.Title.Should().Be("Synthetic report");
        item.Release.Should().Be(Since.AddDays(2));
        item.Length.Should().Be(TimeSpan.FromMinutes(4));
        item.Subjects.Should().Equal("Synthetic Subject");
        item.TitleRegex.Should().Be("^Synthetic: ");
        item.DescriptionRegex.Should().Be("^(?<body>.*)$");
        item.KnownTerms.Should().Equal("SyntheticTerm");
    }

    [Fact(DisplayName =
        "Homepage catalogue: a news report or TV episode whose parent is removed or missing is dropped, even " +
        "when its denormalised parentRemoved is stale, so a removed outlet or show never surfaces on the homepage.")]
    public async Task removed_or_missing_parent_is_dropped()
    {
        // Arrange
        var removedOrganisation = GivenOrganisation("Removed Desk", removed: true);
        var missingOrganisation = new NewsOrganisation("Missing Desk");
        var removedShow = GivenShow("Removed Show", removed: true);
        GivenNewsReports(
            Report(removedOrganisation, "Under removed desk", Since.AddDays(1)),
            Report(missingOrganisation, "Under missing desk", Since.AddDays(1)));
        GivenTvShowEpisodes(TvEpisode(removedShow, "Under removed show", Since.AddDays(1)));

        // Act
        var result = await Sut().GetRecent(Since, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Homepage catalogue: removed, ignored and out-of-window news reports are dropped in memory too, so the " +
        "rule holds even if the Cosmos filter is loosened.")]
    public async Task removed_ignored_and_old_reports_are_dropped()
    {
        // Arrange
        var organisation = GivenOrganisation("Synthetic Desk");
        var removed = Report(organisation, "Removed", Since.AddDays(1));
        removed.Removed = true;
        var ignored = Report(organisation, "Ignored", Since.AddDays(1));
        ignored.Ignored = true;
        var old = Report(organisation, "Old", Since.AddDays(-1));
        var parentRemoved = Report(organisation, "Parent flagged", Since.AddDays(1));
        parentRemoved.ParentRemoved = true;
        var live = Report(organisation, "Live", Since);
        GivenNewsReports(removed, ignored, old, parentRemoved, live);

        // Act
        var result = await Sut().GetRecent(Since, CancellationToken.None);

        // Assert
        result.Select(x => x.Title).Should().Equal("Live");
    }

    [Fact(DisplayName =
        "Homepage catalogue: a recent TV show episode comes back as a TvShowEpisode under the show's name, and " +
        "each parent is read once however many of its episodes are recent.")]
    public async Task tv_show_episode_maps_with_show_name()
    {
        // Arrange
        var show = GivenShow("Synthetic Show");
        GivenTvShowEpisodes(
            TvEpisode(show, "Episode one", Since.AddDays(1)),
            TvEpisode(show, "Episode two", Since.AddDays(2)));

        // Act
        var result = await Sut().GetRecent(Since, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2).And.OnlyContain(x => x.ContentKind == "TvShowEpisode" && x.ParentName == "Synthetic Show");
        result.Select(x => x.Release).Should().BeEquivalentTo([Since.AddDays(1), Since.AddDays(2)]);
        _tvShows.Verify(x => x.GetTvShow(show.Id), Times.Once);
    }

    [Fact(DisplayName =
        "Homepage catalogue: a recent film comes back as a Film under its own name (no parent, no regexes); " +
        "removed, ignored and older films (including a year-only release) are dropped.")]
    public async Task films_are_filtered_and_named_after_themselves()
    {
        // Arrange
        var live = MakeFilm("Synthetic Film", CatalogueRelease.FromDateTimeUtc(Since.AddDays(3)));
        var removed = MakeFilm("Removed Film", CatalogueRelease.FromDateTimeUtc(Since.AddDays(3)));
        removed.Removed = true;
        var ignored = MakeFilm("Ignored Film", CatalogueRelease.FromDateTimeUtc(Since.AddDays(3)));
        ignored.Ignored = true;
        var yearOnly = MakeFilm("Year Film", CatalogueRelease.FromYear(1985));
        GivenFilms(live, removed, ignored, yearOnly);

        // Act
        var result = await Sut().GetRecent(Since, CancellationToken.None);

        // Assert
        var item = result.Should().ContainSingle().Subject;
        item.ContentKind.Should().Be("Film");
        item.Id.Should().Be(live.Id);
        item.ParentName.Should().Be("Synthetic Film");
        item.Title.Should().Be("Synthetic Film");
        item.TitleRegex.Should().BeEmpty();
        item.DescriptionRegex.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Homepage catalogue: the active count and durations add up news reports, TV show episodes and films, " +
        "so the homepage totals still cover items the catalogue migration moves out of Episodes.")]
    public async Task count_and_durations_cover_every_kind()
    {
        // Arrange
        _newsReports
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>(), It.IsAny<Expression<Func<NewsReport, Guid>>>()))
            .Returns(new[] { Guid.NewGuid(), Guid.NewGuid() }.ToAsyncEnumerable());
        _tvShowEpisodes
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>(), It.IsAny<Expression<Func<TvShowEpisode, Guid>>>()))
            .Returns(new[] { Guid.NewGuid() }.ToAsyncEnumerable());
        _films
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>(), It.IsAny<Expression<Func<Film, Guid>>>()))
            .Returns(new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }.ToAsyncEnumerable());
        _newsReports
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>(), It.IsAny<Expression<Func<NewsReport, TimeSpan>>>()))
            .Returns(new[] { TimeSpan.FromMinutes(1) }.ToAsyncEnumerable());
        _tvShowEpisodes
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>(), It.IsAny<Expression<Func<TvShowEpisode, TimeSpan>>>()))
            .Returns(new[] { TimeSpan.FromMinutes(2) }.ToAsyncEnumerable());
        _films
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>(), It.IsAny<Expression<Func<Film, TimeSpan>>>()))
            .Returns(new[] { TimeSpan.FromMinutes(3) }.ToAsyncEnumerable());

        // Act
        var count = await Sut().CountActive(CancellationToken.None);
        var durations = await Sut().GetActiveDurations(CancellationToken.None);

        // Assert
        count.Should().Be(6);
        durations.Should().BeEquivalentTo([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3)]);
    }
}
