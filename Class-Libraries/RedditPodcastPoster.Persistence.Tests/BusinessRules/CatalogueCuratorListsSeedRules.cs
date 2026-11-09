using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class CatalogueCuratorListsSeedRules
{
    [Fact(DisplayName =
        "When the seed allowlist document is built, then it carries the frozen S-008 name count and the fixed LookUps id, " +
        "because the seeder must materialise the compiled set without drifting the singleton key.")]
    public void allowlist_seed_document_matches_static_set()
    {
        // Arrange
        // Act
        var document = CatalogueNewsOrganisationAllowlistLookup.CreateFromSeed();

        // Assert
        document.Id.Should().Be(CatalogueNewsOrganisationAllowlistLookup._Id);
        document.Names.Should().HaveCount(CatalogueNewsOrganisationAllowlist.FrozenCount);
        document.Names.Should().BeEquivalentTo(CatalogueNewsOrganisationAllowlist.Names);
    }

    [Fact(DisplayName =
        "When the stay-podcast seed document is built, then it matches CatalogueStayPodcastNames, " +
        "because stay-podcast names moved into a shared seed without changing the set.")]
    public void stay_podcast_seed_matches_static_set()
    {
        // Arrange
        // Act
        var document = CatalogueStayPodcastNamesLookup.CreateFromSeed();

        // Assert
        document.Id.Should().Be(CatalogueStayPodcastNamesLookup._Id);
        document.Names.Should().BeEquivalentTo(CatalogueStayPodcastNames.Names);
    }

    [Fact(DisplayName =
        "When the TV canonical and channel-alias seed documents are built, then they match the static maps and fixed ids, " +
        "because all four curator lists seed into LookUps the same way.")]
    public void map_seed_documents_match_static_sets()
    {
        // Arrange
        // Act
        var tv = CatalogueTvShowCanonicalNamesLookup.CreateFromSeed();
        var aliases = CatalogueNewsOrganisationChannelAliasesLookup.CreateFromSeed();

        // Assert
        tv.Id.Should().Be(CatalogueTvShowCanonicalNamesLookup._Id);
        tv.ShowNameByPublisherName.Should()
            .BeEquivalentTo(CatalogueTvShowCanonicalNames.ShowNameByPublisherName);
        aliases.Id.Should().Be(CatalogueNewsOrganisationChannelAliasesLookup._Id);
        aliases.OrganisationNameByChannelPublisherName.Should()
            .BeEquivalentTo(CatalogueNewsOrganisationChannelAliases.OrganisationNameByChannelPublisherName);
    }
}
