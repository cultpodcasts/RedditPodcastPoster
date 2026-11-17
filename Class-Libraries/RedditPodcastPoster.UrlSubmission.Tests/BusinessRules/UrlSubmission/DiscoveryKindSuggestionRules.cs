using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Clients;
using RedditPodcastPoster.Tmdb.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class SubmitContentClassifierFromDiscoveryRules
{
    private static readonly Uri YouTubeUrl = new("https://www.youtube.com/watch?v=abcdefghijk");

    [Fact(DisplayName =
        "FromDiscovery maps a catalogue film key to MadeAsFilm and TargetFilmId, because confirming an existing film should attach the URL to that film.")]
    public void from_discovery_maps_a_catalogue_film()
    {
        // Arrange
        var filmId = Guid.NewGuid();

        // Act
        var result = SubmitContentClassifier.FromDiscovery(YouTubeUrl, DiscoveryKindKeys.Film(filmId));

        // Assert
        result.Should().NotBeNull();
        result!.Signals.MadeAsFilm.Should().BeTrue();
        result.TargetFilmId.Should().Be(filmId);
        result.Parent.Should().BeNull();
    }

    [Fact(DisplayName =
        "FromDiscovery maps a TV show key to Series and a TV-show parent, because confirming the show files the item as a TV episode under it.")]
    public void from_discovery_maps_a_tv_show()
    {
        // Arrange
        var showId = Guid.NewGuid();

        // Act
        var result = SubmitContentClassifier.FromDiscovery(YouTubeUrl, DiscoveryKindKeys.TvShow(showId));

        // Assert
        result.Should().NotBeNull();
        result!.Signals.Series.Should().BeTrue();
        result.Parent.Should().BeEquivalentTo(new CatalogueParentTarget(CatalogueParentKind.TvShow, showId));
    }

    [Fact(DisplayName =
        "FromDiscovery maps a news-report key to News and the organisation plus report ids, because confirming the report should enrich that report.")]
    public void from_discovery_maps_a_news_report()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        // Act
        var result = SubmitContentClassifier.FromDiscovery(YouTubeUrl, DiscoveryKindKeys.NewsReport(orgId, reportId));

        // Assert
        result.Should().NotBeNull();
        result!.Signals.News.Should().BeTrue();
        result.Parent.Should().BeEquivalentTo(new CatalogueParentTarget(CatalogueParentKind.NewsOrganisation, orgId));
        result.TargetNewsOrganisationId.Should().Be(orgId);
        result.TargetNewsReportId.Should().Be(reportId);
    }

    [Fact(DisplayName =
        "FromDiscovery maps a TMDB film key to MadeAsFilm and a pre-selected identity, because the curator already picked that title.")]
    public void from_discovery_maps_a_tmdb_film()
    {
        // Arrange
        var key = CatalogueAuthorityCandidate.TmdbKey(42);

        // Act
        var result = SubmitContentClassifier.FromDiscovery(YouTubeUrl, key);

        // Assert
        result.Should().NotBeNull();
        result!.Signals.MadeAsFilm.Should().BeTrue();
        result.IdentityChoice.Should().Be(CatalogueIdentityChoice.Select);
        result.SelectedIdentityKey.Should().Be(key);
    }

    [Fact(DisplayName =
        "FromDiscovery returns null for a missing or unknown key, because the item must stay a podcast.")]
    public void from_discovery_returns_null_when_the_key_is_unknown()
    {
        // Arrange
        // Act
        var missing = SubmitContentClassifier.FromDiscovery(YouTubeUrl, null);
        var unknown = SubmitContentClassifier.FromDiscovery(YouTubeUrl, "not-a-kind-key");

        // Assert
        missing.Should().BeNull();
        unknown.Should().BeNull();
    }
}

public class DiscoveryKindSuggesterRules
{
    [Fact(DisplayName =
        "SuggestAsync returns null when a news organisation already owns the YouTube channel, because known-channel auto-routing must not be prompted.")]
    public async Task mapped_channel_is_not_suggested()
    {
        // Arrange
        var mocker = new AutoMocker();
        var org = new NewsOrganisation("Court TV") { Id = Guid.NewGuid() };
        mocker.GetMock<ICatalogueParentLookup>()
            .Setup(x => x.FindByOwner(It.IsAny<EpisodeUrlOwnerIds>()))
            .ReturnsAsync(CatalogueParentMatch.For(org));
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("A film title", TimeSpan.FromMinutes(95));
        result.YouTubeChannelId = "UCmapped";

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().BeNull();
        mocker.GetMock<ITmdbClient>().Verify(
            x => x.SearchMoviesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "SuggestAsync offers an existing catalogue film when the title and runtime agree, because option B must surface catalogue matches.")]
    public async Task existing_film_title_and_runtime_is_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out var tmdb, out var films, out var shows, out var orgs, out var reports);
        var film = new Film("Father's House") { Id = Guid.NewGuid(), Length = TimeSpan.FromMinutes(96) };
        films.Setup(x => x.GetAll()).Returns(new[] { film }.ToAsyncEnumerable());
        shows.Setup(x => x.GetAll()).Returns(AsyncEmpty<TvShow>());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("Father's House (2024)", TimeSpan.FromMinutes(94));

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().NotBeNull();
        hold!.Target.Should().Be("film");
        hold.Candidates.Should().ContainSingle(c => c.Key == DiscoveryKindKeys.Film(film.Id));
        tmdb.Verify(x => x.SearchMoviesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "SuggestAsync does not offer a TMDB film whose runtime is outside the catalogue duration band, because option B requires title and runtime.")]
    public async Task tmdb_film_outside_duration_band_is_not_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out var tmdb, out var films, out var shows, out var orgs, out _);
        films.Setup(x => x.GetAll()).Returns(AsyncEmpty<Film>());
        shows.Setup(x => x.GetAll()).Returns(AsyncEmpty<TvShow>());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        tmdb.Setup(x => x.SearchMoviesAsync("Night Film", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TmdbSearchHit(9, "Night Film", TmdbTitleKind.Movie, 2020)]);
        tmdb.Setup(x => x.GetMovieMetadataAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieMetadata(9, "Night Film", null, null, 40, new DateOnly(2020, 1, 1), null, []));
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("Night Film", TimeSpan.FromMinutes(95));

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().BeNull();
    }

    [Fact(DisplayName =
        "SuggestAsync offers a TMDB film when the title agrees and the runtime is inside the catalogue duration band.")]
    public async Task tmdb_film_title_and_runtime_is_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out var tmdb, out var films, out var shows, out var orgs, out _);
        films.Setup(x => x.GetAll()).Returns(AsyncEmpty<Film>());
        shows.Setup(x => x.GetAll()).Returns(AsyncEmpty<TvShow>());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        tmdb.Setup(x => x.SearchMoviesAsync("Night Film", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TmdbSearchHit(9, "Night Film", TmdbTitleKind.Movie, 2020)]);
        tmdb.Setup(x => x.GetMovieMetadataAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbMovieMetadata(9, "Night Film", null, "A film.", 93, new DateOnly(2020, 1, 1), null, []));
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("Night Film", TimeSpan.FromMinutes(95));

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().NotBeNull();
        hold!.Candidates.Should().ContainSingle(c => c.Key == CatalogueAuthorityCandidate.TmdbKey(9) && c.Kind == "Film");
    }

    [Fact(DisplayName =
        "SuggestAsync offers an existing TV show when the discovery show name agrees, because a runtime is not required for a show.")]
    public async Task existing_tv_show_name_is_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out _, out var films, out var shows, out var orgs, out _);
        var show = new TvShow("Dateline") { Id = Guid.NewGuid() };
        films.Setup(x => x.GetAll()).Returns(AsyncEmpty<Film>());
        shows.Setup(x => x.GetAll()).Returns(new[] { show }.ToAsyncEnumerable());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("A later episode", TimeSpan.FromMinutes(42), showName: "Dateline");

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().NotBeNull();
        hold!.Target.Should().Be("TV show");
        hold.Candidates.Should().ContainSingle(c => c.Key == DiscoveryKindKeys.TvShow(show.Id));
    }


    [Theory(DisplayName =
        "TitlesAgree uses folded equality, or a word-bounded run of at least eight characters not counting a leading article, so a short title such as Cult or The Cult only matches that exact title.")]
    [InlineData("Father's House (2024)", "Father's House", true)]
    [InlineData("Night Film", "night-film", true)]
    [InlineData("Cult", "Cult", true)]
    [InlineData("The Cult of Personality", "Cult", false)]
    [InlineData("Most titles mention a cult following", "Cult", false)]
    [InlineData("A later Dateline NBC exclusive", "Dateline NBC", true)]
    [InlineData("Documentary", "menta", false)]
    [InlineData("Escaping Rama (Part 3): Life After The Cult", "The Cult", false)]
    [InlineData("The Cult", "The Cult", true)]
    [InlineData("Inside a family business", "A Family", false)]
    [InlineData("Inside an Island Cult", "An Island", false)]
    [InlineData("Inside An Island Cult", "Inside An", true)]
    [InlineData("Life After The Cult explained", "After The", true)]
    [InlineData("Revisiting The Manson Massacre tonight", "The Manson Massacre", true)]
    [InlineData("Watching The Paradise Lost trilogy", "The Paradise Lost", true)]
    public void titles_agree_uses_folded_equality_or_word_bounded_minimum(string left, string right, bool expected)
    {
        // Arrange
        // Act
        var agrees = DiscoveryKindSuggester.TitlesAgree(left, right);

        // Assert
        agrees.Should().Be(expected);
    }

    [Fact(DisplayName =
        "SuggestAsync does not offer a TV show named Cult for an unrelated episode title, because a short title must not match most titles.")]
    public async Task short_tv_show_name_cult_is_not_suggested_for_unrelated_titles()
    {
        // Arrange
        var mocker = SetupUnmapped(out _, out var films, out var shows, out var orgs, out _);
        var show = new TvShow("Cult") { Id = Guid.NewGuid() };
        films.Setup(x => x.GetAll()).Returns(AsyncEmpty<Film>());
        shows.Setup(x => x.GetAll()).Returns(new[] { show }.ToAsyncEnumerable());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("The Cult of Personality", TimeSpan.FromMinutes(42), showName: "A weekly recap");

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().BeNull();
    }

    [Fact(DisplayName =
        "SuggestAsync does not offer a catalogue film for an episode title that only ends in the film's short article-padded AKA, because The Manson Massacre's AKA The Cult must not flag Escaping Rama (Part 3): Life After The Cult (#1457).")]
    public async Task article_padded_short_alternative_title_is_not_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out _, out var films, out _, out _, out _);
        var film = new Film("The Manson Massacre")
        {
            Id = Guid.NewGuid(),
            Length = TimeSpan.FromMinutes(50),
            AlternativeTitles = ["Together Girls", "Girls in Bondage", "The Cult"]
        };
        films.Setup(x => x.GetAll()).Returns(new[] { film }.ToAsyncEnumerable());
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("Escaping Rama (Part 3): Life After The Cult", TimeSpan.FromMinutes(55),
            showName: "Beans & Banter");

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().BeNull();
    }

    [Fact(DisplayName =
        "SuggestAsync still offers a catalogue film whose long alternative title appears word-bounded in the episode title, because only article padding is discounted.")]
    public async Task long_alternative_title_is_still_suggested()
    {
        // Arrange
        var mocker = SetupUnmapped(out _, out var films, out _, out _, out _);
        var film = new Film("The Manson Massacre")
        {
            Id = Guid.NewGuid(),
            Length = TimeSpan.FromMinutes(50),
            AlternativeTitles = ["The Cult", "Girls in Bondage"]
        };
        films.Setup(x => x.GetAll()).Returns(new[] { film }.ToAsyncEnumerable());
        var suggester = mocker.CreateInstance<DiscoveryKindSuggester>();
        var result = UnmappedResult("Girls in Bondage (full film)", TimeSpan.FromMinutes(51));

        // Act
        var hold = await suggester.SuggestAsync(result);

        // Assert
        hold.Should().NotBeNull();
        hold!.Candidates.Should().ContainSingle().Which.Key.Should().Be(DiscoveryKindKeys.Film(film.Id));
    }

    [Fact(DisplayName =
        "DurationAgrees uses the catalogue duration band, so a five-minute gap still matches and a much shorter cut does not.")]
    public void duration_agrees_uses_the_catalogue_band()
    {
        // Arrange
        var film = TimeSpan.FromMinutes(40);
        var close = TimeSpan.FromMinutes(36);
        var far = TimeSpan.FromMinutes(20);

        // Act
        var inBand = DiscoveryKindSuggester.DurationAgrees(film, close);
        var outOfBand = DiscoveryKindSuggester.DurationAgrees(film, far);
        var missing = DiscoveryKindSuggester.DurationAgrees(film, null);

        // Assert
        inBand.Should().BeTrue();
        outOfBand.Should().BeFalse();
        missing.Should().BeTrue();
        CatalogueMatchScorer.GetDurationBand(film, close).Should().Be(TimeSpan.FromMinutes(5));
    }

    private static AutoMocker SetupUnmapped(
        out Mock<ITmdbClient> tmdb,
        out Mock<IFilmRepository> films,
        out Mock<ITvShowRepository> shows,
        out Mock<INewsOrganisationRepository> orgs,
        out Mock<INewsReportRepository> reports)
    {
        var mocker = new AutoMocker();
        mocker.GetMock<ICatalogueParentLookup>()
            .Setup(x => x.FindByOwner(It.IsAny<EpisodeUrlOwnerIds>()))
            .ReturnsAsync((CatalogueParentMatch?)null);
        tmdb = mocker.GetMock<ITmdbClient>();
        films = mocker.GetMock<IFilmRepository>();
        shows = mocker.GetMock<ITvShowRepository>();
        orgs = mocker.GetMock<INewsOrganisationRepository>();
        reports = mocker.GetMock<INewsReportRepository>();
        tmdb.Setup(x => x.SearchMoviesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TmdbSearchHit>());
        films.Setup(x => x.GetAll()).Returns(AsyncEmpty<Film>());
        shows.Setup(x => x.GetAll()).Returns(AsyncEmpty<TvShow>());
        orgs.Setup(x => x.GetAll()).Returns(AsyncEmpty<NewsOrganisation>());
        reports.Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns(AsyncEmpty<NewsReport>());
        return mocker;
    }

    private static DiscoveryResult UnmappedResult(string title, TimeSpan length, string? showName = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeName = title,
            ShowName = showName,
            Length = length,
            YouTubeChannelId = "UCunmapped",
            Urls = new DiscoveryResultUrls { YouTube = new Uri("https://www.youtube.com/watch?v=abcdefghijk") }
        };

    private static IAsyncEnumerable<T> AsyncEmpty<T>() => Array.Empty<T>().ToAsyncEnumerable();
}