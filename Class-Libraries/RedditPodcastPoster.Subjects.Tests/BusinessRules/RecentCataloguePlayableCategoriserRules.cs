using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Categorisation;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules;

public class RecentCataloguePlayableCategoriserRules
{
    private const int CategoriserDays = 7;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly string _defaultSubject;
    private readonly string _matchedSubject;
    private readonly string _descriptionRegex;
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<NewsReport> _reports = [];
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<Podcast> _categorisedAgainst = [];

    public RecentCataloguePlayableCategoriserRules()
    {
        _defaultSubject = _fixture.Create<string>();
        _matchedSubject = _fixture.Create<string>();
        _descriptionRegex = _fixture.Create<string>();

        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = _fixture.CreateDuration(),
            TweetDays = CategoriserDays,
            RedditDays = CategoriserDays,
            BlueSkyDays = CategoriserDays,
            CategoriserDays = CategoriserDays
        }));

        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => _shows.ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Guid id, Expression<Func<TvShowEpisode, bool>> _) =>
                _tvEpisodes.Where(episode => episode.TvShowId == id).ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _savedTvEpisodes.Add(episode))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => _organisations.ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid id, Expression<Func<NewsReport, bool>> _) =>
                _reports.Where(report => report.NewsOrganisationId == id).ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => _savedReports.Add(report))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<ICategoriser>()
            .Setup(x => x.Categorise(It.IsAny<Episode>(), It.IsAny<Podcast>()))
            .ReturnsAsync((Episode episode, Podcast podcast) =>
            {
                _categorisedAgainst.Add(podcast);
                episode.Subjects.Add(podcast.DefaultSubject!);
                episode.Matches.Add(new PlayableSubjectMatch
                {
                    Subject = podcast.DefaultSubject!,
                    Term = string.Empty,
                    Source = SubjectMatchSource.PodcastDefault
                });
                episode.Subjects.Add(_matchedSubject);
                episode.Matches.Add(new PlayableSubjectMatch
                {
                    Subject = _matchedSubject,
                    Term = _matchedSubject,
                    Source = SubjectMatchSource.Title
                });
                return true;
            });
    }

    private RecentCataloguePlayableCategoriser Sut => _mocker.CreateInstance<RecentCataloguePlayableCategoriser>();

    [Fact(DisplayName =
        "When a recent TV episode has no subjects, then it is saved with the show's default subject and the categoriser's match, because the parent indexer stores new TV episodes without subjects.")]
    public async Task recent_tv_episode_without_subjects_is_saved_with_default_and_match()
    {
        // Arrange
        var (show, episode) = CreateTvEpisode();
        var title = episode.Title;
        _shows.Add(show);
        _tvEpisodes.Add(episode);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().Equal(episode.Id);
        _savedTvEpisodes.Should().ContainSingle().Which.Should().BeSameAs(episode);
        episode.Subjects.Should().Equal(_defaultSubject, _matchedSubject);
        episode.Title.Should().Be(title);
        episode.Matches.Should().SatisfyRespectively(
            match =>
            {
                match.Subject.Should().Be(_defaultSubject);
                match.Source.Should().Be(SubjectMatchSource.PodcastDefault);
                match.Term.Should().BeEmpty();
            },
            match =>
            {
                match.Subject.Should().Be(_matchedSubject);
                match.Source.Should().Be(SubjectMatchSource.Title);
                match.Term.Should().Be(_matchedSubject);
            });
        _categorisedAgainst.Should().ContainSingle();
        _categorisedAgainst[0].Id.Should().Be(show.Id);
        _categorisedAgainst[0].Name.Should().Be(show.Name);
        _categorisedAgainst[0].DefaultSubject.Should().Be(show.DefaultSubject);
        _categorisedAgainst[0].DescriptionRegex.Should().Be(_descriptionRegex);
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a recent news report has no subjects, then it is saved on the news report with the organisation's default subject and the categoriser's match, because news reports are not podcast episodes.")]
    public async Task recent_news_report_without_subjects_is_saved_on_the_report()
    {
        // Arrange
        var (organisation, report) = CreateNewsReport();
        _organisations.Add(organisation);
        _reports.Add(report);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().Equal(report.Id);
        _savedReports.Should().ContainSingle().Which.Should().BeSameAs(report);
        report.Subjects.Should().Equal(_defaultSubject, _matchedSubject);
        report.NewsOrganisationId.Should().Be(organisation.Id);
        _categorisedAgainst.Should().ContainSingle();
        _categorisedAgainst[0].Id.Should().Be(organisation.Id);
        _categorisedAgainst[0].DefaultSubject.Should().Be(organisation.DefaultSubject);
        _categorisedAgainst[0].DescriptionRegex.Should().Be(_descriptionRegex);
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a show has no indexing block, then a recent TV episode still receives the show's default subject, because the default subject lives on the publisher.")]
    public async Task show_without_indexing_still_applies_the_default_subject()
    {
        // Arrange
        var (show, episode) = CreateTvEpisode();
        show.Indexing = null;
        _shows.Add(show);
        _tvEpisodes.Add(episode);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().Equal(episode.Id);
        episode.Subjects.Should().Equal(_defaultSubject, _matchedSubject);
        _categorisedAgainst.Should().ContainSingle();
        _categorisedAgainst[0].DefaultSubject.Should().Be(_defaultSubject);
        _categorisedAgainst[0].DescriptionRegex.Should().BeEmpty();
        _savedTvEpisodes.Should().ContainSingle().Which.Should().BeSameAs(episode);
    }

    [Fact(DisplayName =
        "When the categoriser leaves subjects unchanged, then the TV episode is not saved, because an unchanged playable must not be rewritten.")]
    public async Task unchanged_categoriser_does_not_save_the_tv_episode()
    {
        // Arrange
        var (show, episode) = CreateTvEpisode();
        _shows.Add(show);
        _tvEpisodes.Add(episode);
        _mocker.GetMock<ICategoriser>()
            .Setup(x => x.Categorise(It.IsAny<Episode>(), It.IsAny<Podcast>()))
            .ReturnsAsync(false);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().BeEmpty();
        episode.Subjects.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a TV episode already has subjects, or is ignored or removed, then it is left unsaved, because those episodes are not categorised for podcasts either.")]
    public async Task tv_episodes_with_subjects_or_ignored_or_removed_are_left_unsaved()
    {
        // Arrange
        var existingSubject = _fixture.Create<string>();
        var (show, ignored) = CreateTvEpisode(customize: episode => episode.Ignored = true);
        var removed = EpisodeOn(show, DomainTestFixture.UtcDaysAgo(1), episode => episode.Removed = true);
        var categorised = EpisodeOn(show, DomainTestFixture.UtcDaysAgo(1), episode => episode.Subjects.Add(existingSubject));
        _shows.Add(show);
        _tvEpisodes.AddRange([ignored, removed, categorised]);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        ignored.Subjects.Should().BeEmpty();
        removed.Subjects.Should().BeEmpty();
        categorised.Subjects.Should().Equal(existingSubject);
        _categorisedAgainst.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a TV episode was released before the categoriser window, then only the in-window episode is saved, because older playables are outside categoriser days.")]
    public async Task tv_episode_outside_the_categoriser_window_is_not_saved()
    {
        // Arrange
        var (show, recent) = CreateTvEpisode(DomainTestFixture.UtcDaysAgo(1));
        var old = EpisodeOn(show, DomainTestFixture.UtcDaysAgo(CategoriserDays + 1));
        show.LatestReleased = recent.ReleaseSort;
        _shows.Add(show);
        _tvEpisodes.AddRange([recent, old]);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().Equal(recent.Id);
        _savedTvEpisodes.Should().ContainSingle().Which.Should().BeSameAs(recent);
        recent.Subjects.Should().Equal(_defaultSubject, _matchedSubject);
        old.Subjects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a show's latest release is outside the categoriser window, then its TV episodes are not categorised, because only recently active publishers are candidates.")]
    public async Task show_outside_the_categoriser_window_is_not_categorised()
    {
        // Arrange
        var (show, episode) = CreateTvEpisode(DomainTestFixture.UtcDaysAgo(CategoriserDays + 1));
        _shows.Add(show);
        _tvEpisodes.Add(episode);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().BeEmpty();
        episode.Subjects.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        _categorisedAgainst.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a show is removed, then its recent TV episodes are not categorised, because a removed publisher is not a candidate.")]
    public async Task removed_show_is_not_categorised()
    {
        // Arrange
        var (show, episode) = CreateTvEpisode();
        show.Removed = true;
        _shows.Add(show);
        _tvEpisodes.Add(episode);

        // Act
        var updated = await Sut.Categorise();

        // Assert
        updated.Should().BeEmpty();
        episode.Subjects.Should().BeEmpty();
        _savedTvEpisodes.Should().BeEmpty();
        _categorisedAgainst.Should().BeEmpty();
    }

    private (TvShow Show, TvShowEpisode Episode) CreateTvEpisode(
        DateTime? release = null,
        Action<Episode>? customize = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.DefaultSubject = _defaultSubject;
            p.DescriptionRegex = _descriptionRegex;
            p.IndexAllEpisodes = true;
        });
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        var episode = EpisodeOn(show, release ?? DomainTestFixture.UtcDaysAgo(1), customize);
        show.LatestReleased = episode.ReleaseSort;
        return (show, episode);
    }

    private TvShowEpisode EpisodeOn(TvShow show, DateTime release, Action<Episode>? customize = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Id = show.Id;
            p.Name = show.Name;
            p.DefaultSubject = show.DefaultSubject;
        });
        var stored = _fixture.CreateStoredEpisode(podcast, episode =>
        {
            episode.Subjects.Clear();
            episode.Matches.Clear();
            episode.Ignored = false;
            episode.Removed = false;
            episode.ReleaseUtc = release;
            customize?.Invoke(episode);
        });
        return CatalogueParentKindMapper.ToTvShowEpisode(stored, show);
    }

    private (NewsOrganisation Organisation, NewsReport Report) CreateNewsReport()
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.DefaultSubject = _defaultSubject;
            p.DescriptionRegex = _descriptionRegex;
            p.IndexAllEpisodes = true;
        });
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var stored = _fixture.CreateStoredEpisode(podcast, episode =>
        {
            episode.Subjects.Clear();
            episode.Matches.Clear();
            episode.Ignored = false;
            episode.Removed = false;
            episode.ReleaseUtc = DomainTestFixture.UtcDaysAgo(1);
        });
        var report = CatalogueParentKindMapper.ToNewsReport(stored, organisation);
        organisation.LatestReleased = report.ReleaseSort;
        return (organisation, report);
    }
}
