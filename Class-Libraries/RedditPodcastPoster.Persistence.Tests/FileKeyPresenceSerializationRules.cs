using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using RedditPodcastPoster.Text.KnownTerms;

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

    [Fact(DisplayName =
        "When any concrete CosmosSelector type is inspected, then it declares a FileKey JSON property named fileKey if and only if it is IFileKeyed, because a new id-named type with a stray FileKey (or a key-named type without the interface) would silently drift from the backup and publish rules.")]
    public void every_cosmos_selector_has_file_key_iff_file_keyed()
    {
        // Arrange
        var types = typeof(CosmosSelector).Assembly.GetTypes()
            .Append(typeof(KnownTerms))
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(CosmosSelector).IsAssignableFrom(t))
            .ToList();

        // Act
        var mismatches = types
            .Select(t => (Type: t, Property: t.GetProperty(nameof(IFileKeyed.FileKey), BindingFlags.Public | BindingFlags.Instance)))
            .Where(x => typeof(IFileKeyed).IsAssignableFrom(x.Type)
                ? x.Property?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name != "fileKey"
                : x.Property != null)
            .Select(x => x.Type.FullName)
            .ToList();

        // Assert
        types.Should().Contain([typeof(Models.Episodes.Episode), typeof(Person), typeof(HomePageCache), typeof(Film)]);
        mismatches.Should().BeEmpty();
    }

    private static IReadOnlyList<Type> FixedKeySingletonTypes() =>
        typeof(CosmosSelector).Assembly.GetTypes().Append(typeof(KnownTerms))
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(CosmosSelector).IsAssignableFrom(t) &&
                        typeof(IFileKeyed).IsAssignableFrom(t) &&
                        t.GetConstructor(Type.EmptyTypes) != null &&
                        t.GetProperty(nameof(IFileKeyed.FileKey))!.SetMethod == null)
            .ToList();

    public static TheoryData<Type> FixedKeySingletons() => new(FixedKeySingletonTypes());

    [Theory(DisplayName =
        "When a fixed-key singleton is read from JSON carrying a different stored fileKey, then it deserialises without error and keeps its fixed key, because the uploader reads stored look-ups and FileKey is now getter-only on these types.")]
    [MemberData(nameof(FixedKeySingletons))]
    public void fixed_key_singleton_reads_back_with_fixed_key(Type type)
    {
        // Arrange
        var original = CreateWithRequiredStrings(type);
        var json = JsonSerializer.SerializeToNode(original, type, _options)!.AsObject();
        json["fileKey"] = _fixture.CreateTitle();

        // Act
        var read = (IFileKeyed)json.Deserialize(type, _options)!;

        // Assert
        read.FileKey.Should().Be(original.FileKey);
        read.FileKey.Should().NotBe(json["fileKey"]!.GetValue<string>());
    }

    [Fact(DisplayName =
        "When the fixed-key singletons are enumerated, then HomePageCache and KnownTerms are among them, because the read-back theory must not silently run over an empty set.")]
    public void fixed_key_singletons_are_discovered()
    {
        // Arrange
        var expected = new[] { typeof(HomePageCache), typeof(KnownTerms) };

        // Act
        var types = FixedKeySingletonTypes();

        // Assert
        types.Should().Contain(expected);
    }

    private IFileKeyed CreateWithRequiredStrings(Type type)
    {
        var instance = (IFileKeyed)Activator.CreateInstance(type)!;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(x => x.PropertyType == typeof(string) && x.CanWrite &&
                                 x.IsDefined(typeof(System.Runtime.CompilerServices.RequiredMemberAttribute))))
        {
            property.SetValue(instance, _fixture.CreateTitle());
        }

        return instance;
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
