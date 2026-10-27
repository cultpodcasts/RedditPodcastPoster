using System.Reflection;
using System.Text.Json.Serialization;
using AutoFixture.Kernel;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// <see cref="CatalogueIndexingSource"/> hand-mirrors the podcast discovery settings, so these rules pin the
/// mirror: every podcast-declared field is either carried through indexing or explicitly podcast-only.
/// </summary>
public class CatalogueIndexingSourceCoverageRules
{
    /// <summary>
    /// Podcast-declared fields a migrated TV show / news organisation deliberately does not keep. Adding a
    /// podcast field fails <see cref="every_podcast_field_is_carried_or_classified_podcast_only"/> until it is
    /// added to <see cref="CatalogueIndexingSource"/> or listed here.
    /// </summary>
    private static readonly string[] PodcastOnlyFields =
    [
        "youTubeNotificationSubscriptionLeaseExpiry"
    ];

    private readonly DomainTestFixture _fixture = new();

    private static Dictionary<string, PropertyInfo> WireProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
            .Select(p => (Property: p, Name: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .Where(x => x.Name is not null)
            .ToDictionary(x => x.Name!, x => x.Property);

    [Fact(DisplayName =
        "Every field Podcast declares is either kept on CatalogueIndexingSource or listed as podcast-only, because a " +
        "new podcast discovery field must not be silently dropped when a podcast becomes a TV show or news organisation.")]
    public void every_podcast_field_is_carried_or_classified_podcast_only()
    {
        // Arrange
        var podcastFields = WireProperties(typeof(Podcast)).Keys;
        var indexingFields = WireProperties(typeof(CatalogueIndexingSource)).Keys;

        // Act
        var unclassified = podcastFields.Except(indexingFields).Except(PodcastOnlyFields).ToList();
        var unknownIndexing = indexingFields.Except(podcastFields).ToList();

        // Assert
        unclassified.Should().BeEmpty("each podcast field must be carried on indexing or listed as podcast-only");
        unknownIndexing.Should().BeEmpty("indexing wire names must match podcast wire names");
    }

    [Fact(DisplayName =
        "Every indexing field survives podcast -> CatalogueIndexingSource -> transient podcast with its value, " +
        "because the parent indexer must discover exactly as the migrated podcast did.")]
    public void every_indexing_field_round_trips_through_the_transient_podcast()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var context = new SpecimenContext(_fixture.Auto);
        var carried = WireProperties(typeof(Podcast))
            .Where(pair => WireProperties(typeof(CatalogueIndexingSource)).ContainsKey(pair.Key))
            .ToList();
        foreach (var (_, property) in carried)
        {
            property.SetValue(podcast, context.Resolve(property.PropertyType));
        }

        var parent = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var transient = CatalogueIndexingSource.FromPodcast(podcast).ToTransientPodcast(parent);

        // Assert
        carried.Should().NotBeEmpty();
        foreach (var (name, property) in carried)
        {
            property.GetValue(transient).Should().BeEquivalentTo(property.GetValue(podcast), $"'{name}' must round-trip");
        }
    }
}
