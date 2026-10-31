using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.HomePage;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Only documents backed up or published by file key (<see cref="IFileKeyed"/>) store <c>fileKey</c>; playables,
/// people and discovery submissions are named by id and must not write one.
/// </summary>
public class FileKeyPresenceSerializationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    public static TheoryData<string> IdNamedKinds =>
    [
        nameof(Models.Episodes.Episode), nameof(NewsReport), nameof(TvShowEpisode), nameof(Person),
        nameof(DiscoverySubmission)
    ];

    [Theory(DisplayName =
        "When an id-named document (episode, news report, TV episode, person, discovery submission) is serialised, then no fileKey is written, because only IFileKeyed types are backed up by key and the old CosmosSelector default stored an empty fileKey on every one.")]
    [MemberData(nameof(IdNamedKinds))]
    public void id_named_documents_write_no_file_key(string kind)
    {
        // Arrange
        var document = CreateIdNamed(kind);

        // Act
        var json = JsonSerializer.SerializeToNode(document, document.GetType(), _options)!.AsObject();

        // Assert
        document.Should().NotBeAssignableTo<IFileKeyed>();
        json.ContainsKey("fileKey").Should().BeFalse();
        json.ContainsKey("id").Should().BeTrue();
    }

    [Theory(DisplayName =
        "When an id-named document stored before this change still carries an empty fileKey, then it deserialises with its id intact, because the post-deploy cleanup removes those values only after every reader runs this code.")]
    [MemberData(nameof(IdNamedKinds))]
    public void legacy_empty_file_key_still_deserialises(string kind)
    {
        // Arrange
        var document = CreateIdNamed(kind);
        var json = JsonSerializer.SerializeToNode(document, document.GetType(), _options)!.AsObject();
        json["fileKey"] = "";

        // Act
        var read = (CosmosSelector)json.Deserialize(document.GetType(), _options)!;

        // Assert
        read.Id.Should().Be(document.Id);
        read.ModelType.Should().Be(document.ModelType);
    }

    [Fact(DisplayName =
        "When a public catalogue item is serialised, then fileKey is written with its value and read back, because Publisher (IPublicEntity) now owns the file key the public database and backups name files by.")]
    public void public_entity_round_trips_file_key()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { FileKey = "film-" + _fixture.CreateGuid().ToString("N") };

        // Act
        var json = JsonSerializer.SerializeToNode(film, _options)!.AsObject();
        var read = json.Deserialize<Film>(_options)!;

        // Assert
        film.Should().BeAssignableTo<IPublicEntity>();
        json["fileKey"]!.GetValue<string>().Should().Be(film.FileKey);
        read.FileKey.Should().Be(film.FileKey);
    }

    [Fact(DisplayName =
        "When a singleton look-up document is serialised, then its fixed fileKey is still written, because the downloader and stored look-ups rely on it and moving FileKey off CosmosSelector must not change their JSON.")]
    public void singleton_lookup_still_writes_fixed_file_key()
    {
        // Arrange
        var cache = new HomePageCache();

        // Act
        var json = JsonSerializer.SerializeToNode(cache, _options)!.AsObject();

        // Assert
        json["fileKey"]!.GetValue<string>().Should().Be(nameof(HomePageCache));
    }

    private CosmosSelector CreateIdNamed(string kind) =>
        kind switch
        {
            nameof(Models.Episodes.Episode) => _fixture.CreateEpisode(),
            nameof(NewsReport) => new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() },
            nameof(TvShowEpisode) => new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() },
            nameof(Person) => new Person(_fixture.CreateTitle()),
            _ => new DiscoverySubmission { Id = _fixture.CreateGuid(), SubmitterSubject = _fixture.CreateTitle() }
        };
}
