using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.Podcasts;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public sealed class PublicDatabaseValuesRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    [Fact(DisplayName =
        "Public database: when a TV show episode carries a description, length, explicit flag, subjects, service " +
        "urls and external ids, then each of them is published with exactly that value under its own name, " +
        "because consumers read these fields and a swapped or dropped value would otherwise go unnoticed.")]
    public async Task tv_show_episode_values_are_published_as_stored()
    {
        // Arrange
        var show = _scenario.GivenTvShow(s =>
        {
            s.ImdbId = _fixture.Create<string>();
            s.TvdbId = _fixture.Create<int>();
            s.TmdbId = _fixture.Create<int>();
        });
        var youtube = _fixture.Create<Uri>();
        var netflix = _fixture.Create<Uri>();
        var episode = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 4, e =>
        {
            e.Description = _fixture.Create<string>();
            e.Length = TimeSpan.FromMinutes(_fixture.Create<int>());
            e.Explicit = true;
            e.Subjects = [_fixture.Create<string>(), _fixture.Create<string>()];
            e.ImdbId = _fixture.Create<string>();
            e.TvdbId = _fixture.Create<int>();
            e.TmdbId = _fixture.Create<int>();
            e.Services = new Dictionary<string, ServiceLink>
            {
                [StreamingServiceWire.ToKey(StreamingService.Netflix)] = new() { Url = netflix },
                [ServiceKeys.YouTube] = new() { Url = youtube }
            };
        });

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        var published = Published(show.FileKey)["episodes"]!.AsArray().Single()!.AsObject();
        published["id"]!.GetValue<Guid>().Should().Be(episode.Id);
        published["title"]!.GetValue<string>().Should().Be(episode.Title);
        published["description"]!.GetValue<string>().Should().Be(episode.Description);
        published["duration"]!.GetValue<string>().Should().Be(episode.Length.ToString());
        published["explicit"]!.GetValue<bool>().Should().BeTrue();
        published["subjects"]!.AsArray().Select(s => s!.GetValue<string>()).Should().Equal(episode.Subjects);
        published["imdbId"]!.GetValue<string>().Should().Be(episode.ImdbId);
        published["tvdbId"]!.GetValue<long>().Should().Be(episode.TvdbId!.Value);
        published["tmdbId"]!.GetValue<int>().Should().Be(episode.TmdbId!.Value);
        var urls = published["urls"]!.AsObject();
        urls[ServiceKeys.YouTube]!.GetValue<string>().Should().Be(youtube.OriginalString);
        urls[StreamingServiceWire.ToKey(StreamingService.Netflix)]!.GetValue<string>().Should().Be(netflix.OriginalString);
    }

    [Fact(DisplayName =
        "Public database: when a news report carries a description, length, explicit flag, subjects and a service " +
        "url, then each of them is published with exactly that value, because the report is the public item.")]
    public async Task news_report_values_are_published_as_stored()
    {
        // Arrange
        var organisation = _scenario.GivenNewsOrganisation();
        var url = _fixture.Create<Uri>();
        var report = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 2, r =>
        {
            r.Description = _fixture.Create<string>();
            r.Length = TimeSpan.FromMinutes(_fixture.Create<int>());
            r.Explicit = true;
            r.Subjects = [_fixture.Create<string>()];
            r.Services = new Dictionary<string, ServiceLink>
            {
                [StreamingServiceWire.ToKey(StreamingService.BbcIplayer)] = new() { Url = url }
            };
        });

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        var published = Published(organisation.FileKey)["reports"]!.AsArray().Single()!.AsObject();
        published["id"]!.GetValue<Guid>().Should().Be(report.Id);
        published["title"]!.GetValue<string>().Should().Be(report.Title);
        published["description"]!.GetValue<string>().Should().Be(report.Description);
        published["duration"]!.GetValue<string>().Should().Be(report.Length.ToString());
        published["explicit"]!.GetValue<bool>().Should().BeTrue();
        published["subjects"]!.AsArray().Select(s => s!.GetValue<string>()).Should().Equal(report.Subjects);
        published["urls"]![StreamingServiceWire.ToKey(StreamingService.BbcIplayer)]!.GetValue<string>()
            .Should().Be(url.OriginalString);
    }

    [Fact(DisplayName =
        "Public database: when a film carries a description, length, explicit flag, subjects, service urls and " +
        "external ids, then each of them is published with exactly that value, because a film is a file of its own.")]
    public async Task film_values_are_published_as_stored()
    {
        // Arrange
        var url = _fixture.Create<Uri>();
        var film = _scenario.GivenFilm(releasedDaysAgo: 6, f =>
        {
            f.Description = _fixture.Create<string>();
            f.Length = TimeSpan.FromMinutes(_fixture.Create<int>());
            f.Explicit = true;
            f.Subjects = [_fixture.Create<string>(), _fixture.Create<string>()];
            f.ImdbId = _fixture.Create<string>();
            f.TmdbId = _fixture.Create<int>();
            f.Services = new Dictionary<string, ServiceLink> { [ServiceKeys.Apple] = new() { Url = url } };
        });

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        var published = Published(film.FileKey);
        published["id"]!.GetValue<Guid>().Should().Be(film.Id);
        published["name"]!.GetValue<string>().Should().Be(film.Name);
        published["description"]!.GetValue<string>().Should().Be(film.Description);
        published["duration"]!.GetValue<string>().Should().Be(film.Length.ToString());
        published["explicit"]!.GetValue<bool>().Should().BeTrue();
        published["subjects"]!.AsArray().Select(s => s!.GetValue<string>()).Should().Equal(film.Subjects);
        published["imdbId"]!.GetValue<string>().Should().Be(film.ImdbId);
        published["tmdbId"]!.GetValue<int>().Should().Be(film.TmdbId!.Value);
        published["urls"]![ServiceKeys.Apple]!.GetValue<string>().Should().Be(url.OriginalString);
    }

    [Theory(DisplayName =
        "Public database: when TV show episodes share the same release, then they are ordered by id whichever " +
        "order they are stored in, because the file must not change between runs when nothing changed.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task tv_show_episodes_released_together_are_ordered_by_id_in_any_storage_order(bool reversed)
    {
        // Arrange
        var show = _scenario.GivenTvShow();
        var release = CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDaysAgo(7)));
        var episodes = Enumerable.Range(0, 4)
            .Select(_ => _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 7, e => e.Release = release))
            .ToList();
        if (reversed)
        {
            _scenario.TvShowEpisodes.Reverse();
        }

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        Published(show.FileKey)["episodes"]!.AsArray().Select(e => e!["id"]!.GetValue<Guid>())
            .Should().Equal(episodes.Select(e => e.Id).Order());
    }

    [Theory(DisplayName =
        "Public database: when news reports share the same release, then they are ordered by id whichever " +
        "order they are stored in, because the file must not change between runs when nothing changed.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task news_reports_released_together_are_ordered_by_id_in_any_storage_order(bool reversed)
    {
        // Arrange
        var organisation = _scenario.GivenNewsOrganisation();
        var release = CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDaysAgo(7)));
        var reports = Enumerable.Range(0, 4)
            .Select(_ => _scenario.GivenNewsReport(organisation, releasedDaysAgo: 7, r => r.Release = release))
            .ToList();
        if (reversed)
        {
            _scenario.NewsReports.Reverse();
        }

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        Published(organisation.FileKey)["reports"]!.AsArray().Select(r => r!["id"]!.GetValue<Guid>())
            .Should().Equal(reports.Select(r => r.Id).Order());
    }

    [Fact(DisplayName =
        "Public database: when a TV show's episodes have a year, a date and a date-time release, then they are " +
        "ordered by the instant each release stands for (a year is 1 January, a date is midnight), because that " +
        "is the order the catalogue itself sorts by.")]
    public async Task mixed_release_precisions_are_ordered_by_their_sort_instant()
    {
        // Arrange
        var show = _scenario.GivenTvShow();
        var day = DomainTestFixture.UtcDaysAgo(10);
        var dateTime = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 10,
            e => e.Release = CatalogueRelease.FromDateTimeUtc(day.AddHours(15)));
        var sameDay = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 10,
            e => e.Release = CatalogueRelease.FromDate(DateOnly.FromDateTime(day)));
        var earlierDate = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 20,
            e => e.Release = CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDaysAgo(20))));
        var yearOnly = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1,
            e => e.Release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 2));
        var undated = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1, e => e.Release = null);
        _scenario.TvShowEpisodes.Reverse();

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        Published(show.FileKey)["episodes"]!.AsArray().Select(e => e!["id"]!.GetValue<Guid>())
            .Should().Equal(dateTime.Id, sameDay.Id, earlierDate.Id, yearOnly.Id, undated.Id);
    }

    [Fact(DisplayName =
        "Public database: when every episode or report of a TV show or news organisation is flagged removed, " +
        "then the file is still written with an empty list, because that is what a podcast whose episodes are " +
        "all removed does and the existing podcast bytes must not change.")]
    public async Task parents_whose_children_are_all_removed_are_published_with_an_empty_list()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e => e.Removed = true);
        var show = _scenario.GivenTvShow();
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1, e => e.Removed = true);
        var organisation = _scenario.GivenNewsOrganisation();
        _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1, r => r.Removed = true);

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        Published(podcast.FileKey)["episodes"]!.AsArray().Should().BeEmpty();
        Published(show.FileKey)["episodes"]!.AsArray().Should().BeEmpty();
        Published(organisation.FileKey)["reports"]!.AsArray().Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Public database: when releases are a year, a date and a date-time, then each is published as a string " +
        "that keeps its precision (the year as text, yyyy-MM-dd, and ISO-8601 Zulu) on TV show episodes, news " +
        "reports and films alike, because a consumer must read one type whatever the precision.")]
    public async Task release_is_always_a_string_that_keeps_its_precision()
    {
        // Arrange
        var year = DomainTestFixture.UtcToday.Year - 3;
        var date = DateOnly.FromDateTime(DomainTestFixture.UtcDaysAgo(40));
        var dateTime = DomainTestFixture.UtcDaysAgo(8).AddHours(14).AddMinutes(30);
        var show = _scenario.GivenTvShow();
        var byYear = _scenario.GivenTvShowEpisode(show, 1, e => e.Release = CatalogueRelease.FromYear(year));
        var byDate = _scenario.GivenTvShowEpisode(show, 1, e => e.Release = CatalogueRelease.FromDate(date));
        var byDateTime = _scenario.GivenTvShowEpisode(show, 1, e => e.Release = CatalogueRelease.FromDateTimeUtc(dateTime));
        var organisation = _scenario.GivenNewsOrganisation();
        _scenario.GivenNewsReport(organisation, 1, r => r.Release = CatalogueRelease.FromYear(year));
        var film = _scenario.GivenFilm(customize: f => f.Release = CatalogueRelease.FromDateTimeUtc(dateTime));
        var unreleased = _scenario.GivenFilm(customize: f => f.Release = null);

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        var episodes = Published(show.FileKey)["episodes"]!.AsArray().ToDictionary(e => e!["id"]!.GetValue<Guid>());
        var zulu = dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        episodes[byYear.Id]!["release"]!.GetValueKind().Should().Be(JsonValueKind.String);
        episodes[byYear.Id]!["release"]!.GetValue<string>().Should().Be(year.ToString(CultureInfo.InvariantCulture));
        episodes[byDate.Id]!["release"]!.GetValue<string>().Should().Be(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        episodes[byDateTime.Id]!["release"]!.GetValue<string>().Should().Be(zulu);
        Published(organisation.FileKey)["reports"]!.AsArray().Single()!["release"]!.GetValue<string>()
            .Should().Be(year.ToString(CultureInfo.InvariantCulture));
        Published(film.FileKey)["release"]!.GetValue<string>().Should().Be(zulu);
        Published(unreleased.FileKey).ContainsKey("release").Should().BeFalse();
    }

    [Fact(DisplayName =
        "Public database: when an episode is on both BBC iPlayer and BBC Sounds, then a podcast file keeps its single " +
        "bbc url (iPlayer first) while a TV show file lists bbcIplayer and bbcSounds separately, because the new " +
        "kinds publish every catalogue service key as it is and the two shapes differ on purpose.")]
    public async Task podcast_urls_merge_the_bbc_services_and_the_new_kinds_do_not()
    {
        // Arrange
        var iplayer = _fixture.Create<Uri>();
        var sounds = _fixture.Create<Uri>();
        var iplayerKey = StreamingServiceWire.ToKey(StreamingService.BbcIplayer);
        var soundsKey = StreamingServiceWire.ToKey(StreamingService.BbcSounds);
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e =>
        {
            EpisodeServicePresence.Upsert(e, iplayerKey, iplayer, null);
            EpisodeServicePresence.Upsert(e, soundsKey, sounds, null);
        });
        var show = _scenario.GivenTvShow();
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1, e => e.Services = new Dictionary<string, ServiceLink>
        {
            [iplayerKey] = new() { Url = iplayer },
            [soundsKey] = new() { Url = sounds }
        });

        // Act
        await _scenario.PublishFirstTime();

        // Assert
        var podcastUrls = Published(podcast.FileKey)["episodes"]!.AsArray().Single()!["urls"]!.AsObject();
        podcastUrls.Select(u => u.Key).Should().Equal("bbc");
        podcastUrls["bbc"]!.GetValue<string>().Should().Be(iplayer.OriginalString);
        var showUrls = Published(show.FileKey)["episodes"]!.AsArray().Single()!["urls"]!.AsObject();
        showUrls.Select(u => u.Key).Should().Equal(iplayerKey, soundsKey);
        showUrls[soundsKey]!.GetValue<string>().Should().Be(sounds.OriginalString);
    }
    private JsonObject Published(string fileKey) => JsonNode.Parse(_scenario.Files[fileKey])!.AsObject();
}
