using System.Globalization;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.Podcasts;

namespace CultPodcasts.DatabasePublisher.Tests.BusinessRules;

[Collection(PublisherCollection.Name)]
public class PublicDatabaseKindsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly PublisherScenario _scenario = new();

    [Fact(DisplayName =
        "Public database: when a podcast with platform ids and three episodes (ids, subjects, urls on every " +
        "service, a blank description) is published, then the whole file is exactly the existing podcast text, " +
        "because consumers track these files in git and publishing other kinds must not change a single podcast byte.")]
    public async Task podcast_file_keeps_the_existing_text_byte_for_byte()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast(p =>
        {
            p.AppleId = _fixture.Create<int>();
            p.SpotifyId = _fixture.Create<string>();
            p.YouTubeChannelId = _fixture.Create<string>();
            p.YouTubePlaylistId = _fixture.Create<string>();
        });
        var allServices = _scenario.GivenEpisode(podcast, releasedDaysAgo: 1, e =>
        {
            e.Ids = new EpisodeIds
            {
                Spotify = _fixture.Create<string>(),
                Apple = _fixture.Create<int>(),
                YouTube = _fixture.Create<string>()
            };
            EpisodeServicePresence.Upsert(e, ServiceKeys.Spotify, _fixture.Create<Uri>(), null);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Apple, _fixture.Create<Uri>(), null);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.Create<Uri>(), null);
        });
        var bbcWithSubjects = _scenario.GivenEpisode(podcast, releasedDaysAgo: 3, e =>
        {
            e.Description = " ";
            e.Subjects = [_fixture.Create<string>(), _fixture.Create<string>()];
            EpisodeServicePresence.Upsert(
                e, StreamingServiceWire.ToKey(StreamingService.BbcSounds), _fixture.Create<Uri>(), null);
        });
        var archiveExplicit = _scenario.GivenEpisode(podcast, releasedDaysAgo: 9, e =>
        {
            e.Explicit = true;
            EpisodeServicePresence.Upsert(
                e, StreamingServiceWire.ToKey(StreamingService.InternetArchive), _fixture.Create<Uri>(), null);
        });
        var expected = $$"""
                         {
                           "id": "{{podcast.Id}}",
                           "name": "{{podcast.Name}}",
                           "spotifyId": "{{podcast.SpotifyId}}",
                           "appleId": {{podcast.AppleId}},
                           "youTubeChannelId": "{{podcast.YouTubeChannelId}}",
                           "youTubePlaylistId": "{{podcast.YouTubePlaylistId}}",
                           "episodes": [
                             {
                               "id": "{{allServices.Id}}",
                               "title": "{{allServices.Title}}",
                               "description": "{{allServices.Description}}",
                               "release": "{{Release(allServices)}}",
                               "duration": "{{allServices.Length}}",
                               "explicit": false,
                               "spotifyId": "{{allServices.Ids!.Spotify}}",
                               "appleId": {{allServices.Ids.Apple}},
                               "youTubeId": "{{allServices.Ids.YouTube}}",
                               "urls": {
                                 "spotify": "{{Url(allServices, ServiceKeys.Spotify)}}",
                                 "apple": "{{Url(allServices, ServiceKeys.Apple)}}",
                                 "youtube": "{{Url(allServices, ServiceKeys.YouTube)}}"
                               }
                             },
                             {
                               "id": "{{bbcWithSubjects.Id}}",
                               "title": "{{bbcWithSubjects.Title}}",
                               "release": "{{Release(bbcWithSubjects)}}",
                               "duration": "{{bbcWithSubjects.Length}}",
                               "explicit": false,
                               "subjects": [
                                 "{{bbcWithSubjects.Subjects[0]}}",
                                 "{{bbcWithSubjects.Subjects[1]}}"
                               ],
                               "urls": {
                                 "bbc": "{{Url(bbcWithSubjects, StreamingServiceWire.ToKey(StreamingService.BbcSounds))}}"
                               }
                             },
                             {
                               "id": "{{archiveExplicit.Id}}",
                               "title": "{{archiveExplicit.Title}}",
                               "description": "{{archiveExplicit.Description}}",
                               "release": "{{Release(archiveExplicit)}}",
                               "duration": "{{archiveExplicit.Length}}",
                               "explicit": true,
                               "urls": {
                                 "internetArchive": "{{Url(archiveExplicit, StreamingServiceWire.ToKey(StreamingService.InternetArchive))}}"
                               }
                             }
                           ]
                         }
                         """;

        // Act
        await _scenario.Publish();

        // Assert
        _scenario.Writer.Files.Keys.Should().BeEquivalentTo(podcast.FileKey);
        Normalise(_scenario.Writer.Files[podcast.FileKey]).Should().Be(Normalise(expected));
    }
    [Fact(DisplayName =
        "Public database: when a TV show has live and removed episodes, then it is published as tvshow-{slug}.json " +
        "with the show's name and external ids and only the live episodes, newest first, with their service urls " +
        "sorted by key, because the show is the file and removed episodes must not reach the public repo.")]
    public async Task tv_show_is_published_with_its_live_episodes_newest_first()
    {
        // Arrange
        var show = _scenario.GivenTvShow(s =>
        {
            s.ImdbId = _fixture.Create<string>();
            s.TvdbId = _fixture.Create<int>();
            s.TmdbId = _fixture.Create<int>();
        });
        var oldest = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 30);
        var newest = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 2, e =>
        {
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.Create<Uri>() },
                [ServiceKeys.Apple] = new() { Url = _fixture.Create<Uri>() },
                [ServiceKeys.Spotify] = new() { Image = _fixture.Create<Uri>() }
            };
        });
        var middle = _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 10);
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 5, e => e.Removed = true);

        // Act
        await _scenario.Publish();

        // Assert
        var file = ReadFile(show.FileKey);
        show.FileKey.Should().StartWith(FileKeyFactory.TvShowPrefix);
        PropertyNames(file).Should().Equal("id", "name", "imdbId", "tvdbId", "tmdbId", "episodes");
        file["id"]!.GetValue<Guid>().Should().Be(show.Id);
        file["name"]!.GetValue<string>().Should().Be(show.Name);
        file["imdbId"]!.GetValue<string>().Should().Be(show.ImdbId);
        file["tvdbId"]!.GetValue<long>().Should().Be(show.TvdbId!.Value);
        file["tmdbId"]!.GetValue<int>().Should().Be(show.TmdbId!.Value);
        var episodes = file["episodes"]!.AsArray();
        episodes.Select(e => e!["id"]!.GetValue<Guid>()).Should().Equal(newest.Id, middle.Id, oldest.Id);
        PropertyNames(episodes[0]!.AsObject()).Should()
            .Equal("id", "title", "description", "release", "duration", "explicit", "urls");
        episodes[0]!["title"]!.GetValue<string>().Should().Be(newest.Title);
        PropertyNames(episodes[0]!["urls"]!.AsObject()).Should().Equal(ServiceKeys.Apple, ServiceKeys.YouTube);
    }

    [Fact(DisplayName =
        "Public database: when a news organisation has live and removed reports, then it is published as " +
        "news-{slug}.json with the organisation's name and only the live reports, newest first, because news " +
        "organisations are the container the way podcasts are.")]
    public async Task news_organisation_is_published_with_its_live_reports_newest_first()
    {
        // Arrange
        var organisation = _scenario.GivenNewsOrganisation();
        var older = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 9);
        var newer = _scenario.GivenNewsReport(organisation, releasedDaysAgo: 1);
        _scenario.GivenNewsReport(organisation, releasedDaysAgo: 4, r => r.Removed = true);

        // Act
        await _scenario.Publish();

        // Assert
        var file = ReadFile(organisation.FileKey);
        organisation.FileKey.Should().StartWith(FileKeyFactory.NewsOrganisationPrefix);
        PropertyNames(file).Should().Equal("id", "name", "reports");
        file["id"]!.GetValue<Guid>().Should().Be(organisation.Id);
        file["name"]!.GetValue<string>().Should().Be(organisation.Name);
        file["reports"]!.AsArray().Select(r => r!["id"]!.GetValue<Guid>()).Should().Equal(newer.Id, older.Id);
    }

    [Fact(DisplayName =
        "Public database: when a film is live, then it is published as film-{slug}.json holding the film itself " +
        "(no episodes), because a film has no parent and its file key is the reserved film- prefix plus its title.")]
    public async Task film_is_published_as_a_standalone_file()
    {
        // Arrange
        var film = _scenario.GivenFilm(releasedDaysAgo: 12, f =>
        {
            f.ImdbId = _fixture.Create<string>();
            f.TmdbId = _fixture.Create<int>();
        });

        // Act
        await _scenario.Publish();

        // Assert
        var file = ReadFile(film.FileKey);
        film.FileKey.Should().StartWith(FileKeyFactory.FilmPrefix);
        PropertyNames(file).Should().Equal(
            "id", "name", "description", "release", "duration", "explicit", "imdbId", "tmdbId", "urls");
        file["id"]!.GetValue<Guid>().Should().Be(film.Id);
        file["name"]!.GetValue<string>().Should().Be(film.Name);
        file["imdbId"]!.GetValue<string>().Should().Be(film.ImdbId);
        file["tmdbId"]!.GetValue<int>().Should().Be(film.TmdbId!.Value);
    }

    [Fact(DisplayName =
        "Public database: when a TV show, news organisation or film is removed, or a show or organisation has no " +
        "episodes or reports, then no file is written for it, because only live items with content are published " +
        "- the same rule podcasts follow.")]
    public async Task removed_and_empty_items_are_not_published()
    {
        // Arrange
        var liveShow = _scenario.GivenTvShow();
        _scenario.GivenTvShowEpisode(liveShow, releasedDaysAgo: 1);
        var removedShow = _scenario.GivenTvShow(s => s.Removed = true);
        _scenario.GivenTvShowEpisode(removedShow, releasedDaysAgo: 1);
        _scenario.GivenTvShow();
        var liveOrganisation = _scenario.GivenNewsOrganisation();
        _scenario.GivenNewsReport(liveOrganisation, releasedDaysAgo: 1);
        var removedOrganisation = _scenario.GivenNewsOrganisation(o => o.Removed = true);
        _scenario.GivenNewsReport(removedOrganisation, releasedDaysAgo: 1);
        _scenario.GivenNewsOrganisation();
        var liveFilm = _scenario.GivenFilm();
        _scenario.GivenFilm(customize: f => f.Removed = true);

        // Act
        await _scenario.Publish();

        // Assert
        _scenario.Writer.Files.Keys.Should().BeEquivalentTo(liveShow.FileKey, liveOrganisation.FileKey, liveFilm.FileKey);
    }

    [Fact(DisplayName =
        "Public database: when two films share a file key, then the run fails naming the key and writes no file " +
        "at all, because a silent overwrite would swap one item's history for another's in the public repo.")]
    public async Task duplicate_file_keys_fail_before_anything_is_written()
    {
        // Arrange
        var podcast = _scenario.GivenPodcast();
        _scenario.GivenEpisode(podcast, releasedDaysAgo: 1);
        var title = _fixture.CreateTitle();
        var first = _scenario.GivenFilm(customize: f => f.FileKey = FileKeyFactory.GetFilmFileKey(title));
        _scenario.GivenFilm(customize: f => f.FileKey = FileKeyFactory.GetFilmFileKey(title));

        // Act
        var run = () => _scenario.Publish();

        // Assert
        (await run.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{first.FileKey}*");
        _scenario.Writer.Files.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Public database: when two file keys differ only by case, then the run fails, because the files would be " +
        "the same file on a case-insensitive checkout.")]
    public async Task file_keys_differing_only_by_case_fail()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        _scenario.GivenFilm(customize: f => f.FileKey = FileKeyFactory.GetFilmFileKey(title));
        _scenario.GivenFilm(customize: f => f.FileKey = FileKeyFactory.GetFilmFileKey(title).ToUpperInvariant());

        // Act
        var run = () => _scenario.Publish();

        // Assert
        await run.Should().ThrowAsync<InvalidOperationException>();
        _scenario.Writer.Files.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Public database: when a TV show has an empty file key, then the run fails naming its id and writes " +
        "nothing, because a file cannot be created without a key.")]
    public async Task empty_file_key_fails_naming_the_item()
    {
        // Arrange
        var show = _scenario.GivenTvShow(s => s.FileKey = " ");
        _scenario.GivenTvShowEpisode(show, releasedDaysAgo: 1);

        // Act
        var run = () => _scenario.Publish();

        // Assert
        (await run.Should().ThrowAsync<InvalidOperationException>()).WithMessage($"*{show.Id}*");
        _scenario.Writer.Files.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Public database: when an item's file key does not carry its kind's reserved prefix (a film without " +
        "film-, or a podcast with one), then the run fails, because the prefix is how consumers and the " +
        "publisher keep the kinds apart in one flat folder.")]
    public async Task file_keys_must_carry_their_kinds_prefix()
    {
        // Arrange
        _scenario.GivenFilm(customize: f => f.FileKey = FileKeyFactory.GetFileKey(f.Name));
        _scenario.GivenPodcast(p => p.FileKey = FileKeyFactory.TvShowPrefix + FileKeyFactory.GetFileKey(p.Name));

        // Act
        var run = () => _scenario.Publish();

        // Assert
        (await run.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*prefix*");
        _scenario.Writer.Files.Should().BeEmpty();
    }

    private JsonObject ReadFile(string fileKey) => JsonNode.Parse(_scenario.Writer.Files[fileKey])!.AsObject();

    private static IEnumerable<string> PropertyNames(JsonObject node) => node.Select(p => p.Key);

    private static string Normalise(string text) => text.Replace("\r\n", "\n");

    private static string Release(Episode episode) =>
        episode.ReleaseUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Url(Episode episode, string key) =>
        EpisodeServicePresence.TryGetUrl(episode, key)!.OriginalString;
}
