using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using FluentAssertions;
using CultPodcasts.DatabasePublisher.PublicModels;
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
        "When any concrete CosmosSelector type in any assembly that references the models is inspected, then it declares a FileKey JSON property named fileKey if and only if it is IFileKeyed, because a new id-named type with a stray FileKey (or a key-named type without the interface) would silently drift from the backup and publish rules.")]
    public void every_cosmos_selector_has_file_key_iff_file_keyed()
    {
        // Arrange
        var types = CosmosSelectorAssemblyCatalog.ConcreteSelectorTypes()
            .Where(t => t.IsDefined(typeof(CosmosSelectorAttribute), true))
            .ToList();

        // Act
        var mismatches = types
            .Select(t => (Type: t, Property: FileKeyProperty(t)))
            .Where(x => typeof(IFileKeyed).IsAssignableFrom(x.Type)
                ? x.Property?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name != "fileKey"
                : x.Property != null)
            .Select(x => x.Type.FullName)
            .ToList();

        // Assert
        types.Should().Contain([
            typeof(Models.Episodes.Episode), typeof(Person), typeof(HomePageCache), typeof(Film), typeof(KnownTerms),
            typeof(global::Azure.Activity), typeof(EpisodeServiceBackfill.LeftoverEpisodeDocument)
        ]);
        mismatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a concrete CosmosSelector without a [CosmosSelector] document attribute (a public-database projection) is inspected, then it is IFileKeyed and its FileKey is [JsonIgnore], because the publisher names each public file by key but the key is Cosmos bookkeeping that must not appear in the public JSON.")]
    public void public_projections_are_file_keyed_without_writing_file_key()
    {
        // Arrange
        var projections = CosmosSelectorAssemblyCatalog.ConcreteSelectorTypes()
            .Where(t => !t.IsDefined(typeof(CosmosSelectorAttribute), true))
            .ToList();

        // Act
        var mismatches = projections
            .Where(t => !typeof(IFileKeyed).IsAssignableFrom(t) ||
                        FileKeyProperty(t)?.GetCustomAttribute<JsonIgnoreAttribute>() is not
                            { Condition: JsonIgnoreCondition.Always })
            .Select(t => t.FullName)
            .ToList();

        // Assert
        projections.Should().Contain([typeof(PublicPodcast), typeof(PublicFilm), typeof(PublicTvShow), typeof(PublicNewsOrganisation)]);
        mismatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the source tree is scanned for production classes deriving from CosmosSelector (or a subclass of it), then every declaring project's assembly is among those the file-key guards inspect, and the models, KnownTerms, Activity and public-database assemblies are among them, because a project the guards cannot see would let a stray or missing fileKey through unchecked.")]
    public void every_project_declaring_cosmos_selectors_is_inspected()
    {
        // Arrange
        var expected = new[] { typeof(CosmosSelector), typeof(KnownTerms), typeof(global::Azure.Activity), typeof(PublicPodcast) }
            .Select(t => t.Assembly.GetName().Name!);

        // Act
        var unscanned = CosmosSelectorAssemblyCatalog.UnscannedProductionProjects(out var declaring);

        // Assert
        declaring.Should().Contain(expected);
        unscanned.Should().BeEmpty("add a ProjectReference to each listed project in RedditPodcastPoster.Persistence.Tests");
    }

    public static TheoryData<string, bool> ClassHeaders() => new()
    {
        { "public sealed class Thing : CosmosSelector, IFileKeyed { }", true },
        { "public sealed class Thing(Guid id) : PublicCatalogueEntity(id, ModelType.Film) { }", true },
        { "internal class Thing<T> : Ns.CosmosSelector where T : class { }", true },
        { "public class Repo<T> where T : CosmosSelector { }", false },
        { "public class Thing : IComparable // : CosmosSelector\n{ }", false },
        { "public class Thing(\n    ILogger<Thing> logger,\n    int count) : Base<int>, IThing { }", false }
    };

    [Theory(DisplayName =
        "When a class header is checked for a CosmosSelector base, then only its base list counts (primary-constructor arguments, generic arguments, where-constraints and comments are ignored), because the source-tree cross-check must neither miss a subclass nor flag a generic constraint.")]
    [MemberData(nameof(ClassHeaders))]
    public void class_header_detection_reads_only_the_base_list(string source, bool expected)
    {
        // Arrange
        var baseNames = new HashSet<string> { nameof(CosmosSelector), nameof(PublicCatalogueEntity) };

        // Act
        var declares = CosmosSelectorAssemblyCatalog.DeclaresSubclassOf(source, baseNames);

        // Assert
        declares.Should().Be(expected);
    }

    private static PropertyInfo? FileKeyProperty(Type type) =>
        type.GetProperty(nameof(IFileKeyed.FileKey), BindingFlags.Public | BindingFlags.Instance);

    private static IReadOnlyList<Type> FixedKeySingletonTypes() =>
        CosmosSelectorAssemblyCatalog.ConcreteSelectorTypes()
            .Where(t => typeof(IFileKeyed).IsAssignableFrom(t) &&
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
