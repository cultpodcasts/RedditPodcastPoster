using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Lookups;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class CatalogueCuratorListsProviderRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public CatalogueCuratorListsProviderRules()
    {
        _mocker.Use(NullLogger<CatalogueCuratorListsProvider>.Instance);
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueNewsOrganisationAllowlistLookup())
            .ReturnsAsync((CatalogueNewsOrganisationAllowlistLookup?)null);
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueStayPodcastNamesLookup())
            .ReturnsAsync((CatalogueStayPodcastNamesLookup?)null);
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueTvShowCanonicalNamesLookup())
            .ReturnsAsync((CatalogueTvShowCanonicalNamesLookup?)null);
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueNewsOrganisationChannelAliasesLookup())
            .ReturnsAsync((CatalogueNewsOrganisationChannelAliasesLookup?)null);
    }

    [Fact(DisplayName =
        "When all four catalogue curator LookUps documents are missing, then the provider returns the compiled static seeds, " +
        "because the binary remains the fallback until a curator seeds LookUps.")]
    public async Task missing_lookups_fall_back_to_seed()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueCuratorListsProvider>();

        // Act
        var lists = await sut.GetAsync();

        // Assert
        lists.NewsOrganisationAllowlist.Should().BeEquivalentTo(CatalogueNewsOrganisationAllowlist.Names);
        lists.StayPodcastPublisherNames.Should().BeEquivalentTo(CatalogueStayPodcastNames.Names);
        lists.TvShowNameByPublisherName.Should().BeEquivalentTo(CatalogueTvShowCanonicalNames.ShowNameByPublisherName);
        lists.OrganisationNameByChannelPublisherName.Should()
            .BeEquivalentTo(CatalogueNewsOrganisationChannelAliases.OrganisationNameByChannelPublisherName);
    }

    [Fact(DisplayName =
        "When LookUps stores a custom S-008 allowlist entry, then IsAllowlisted is true for that name and false for a seed-only name, " +
        "because an explicit LookUps document replaces the seed set for that list.")]
    public async Task lookups_allowlist_replaces_seed_set()
    {
        // Arrange
        var allowlisted = _fixture.CreateTitle();
        var seedOnly = CatalogueNewsOrganisationAllowlist.Names.First();
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueNewsOrganisationAllowlistLookup())
            .ReturnsAsync(new CatalogueNewsOrganisationAllowlistLookup { Names = [allowlisted] });
        var sut = _mocker.CreateInstance<CatalogueCuratorListsProvider>();

        // Act
        var lists = await sut.GetAsync();

        // Assert
        lists.IsAllowlisted(allowlisted).Should().BeTrue();
        lists.IsAllowlisted(seedOnly).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When LookUps stores a TV canonical mapping, then ShowNameFor and ParentKindMapper use that show name, " +
        "because migrate and submit share the provider snapshot.")]
    public async Task lookups_tv_canonical_name_is_used_by_mapper()
    {
        // Arrange
        var publisherName = _fixture.CreateTitle();
        var showName = _fixture.CreateTitle();
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueTvShowCanonicalNamesLookup())
            .ReturnsAsync(new CatalogueTvShowCanonicalNamesLookup
            {
                ShowNameByPublisherName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [publisherName] = showName
                }
            });
        var podcast = _fixture.CreatePodcast(p => p.Name = publisherName);
        var sut = _mocker.CreateInstance<CatalogueCuratorListsProvider>();

        // Act
        var lists = await sut.GetAsync();
        var show = CatalogueParentKindMapper.ToTvShow(podcast, lists);

        // Assert
        lists.ShowNameFor(publisherName).Should().Be(showName);
        show.Name.Should().Be(showName);
    }

    [Fact(DisplayName =
        "When LookUps stores a news channel alias, then OrganisationNameFor maps the extra channel onto the canonical outlet, " +
        "because migrate apply must parent reports onto the outlet already in the corpus.")]
    public async Task lookups_channel_alias_maps_to_organisation()
    {
        // Arrange
        var channelName = _fixture.CreateTitle();
        var organisationName = _fixture.CreateTitle();
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueNewsOrganisationChannelAliasesLookup())
            .ReturnsAsync(new CatalogueNewsOrganisationChannelAliasesLookup
            {
                OrganisationNameByChannelPublisherName =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [channelName] = organisationName
                    }
            });
        var podcast = _fixture.CreatePodcast(p => p.Name = channelName);
        var sut = _mocker.CreateInstance<CatalogueCuratorListsProvider>();

        // Act
        var lists = await sut.GetAsync();
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast, lists);

        // Assert
        lists.OrganisationNameFor(channelName).Should().Be(organisationName);
        organisation.Name.Should().Be(organisationName);
    }

    [Fact(DisplayName =
        "When a LookUps name list document exists but is empty, then the provider falls back to the seed for that list, " +
        "because an empty curator document must not wipe the allowlist or stay-podcast set.")]
    public async Task empty_lookups_document_falls_back_to_seed()
    {
        // Arrange
        _mocker.GetMock<ILookupRepository>()
            .Setup(x => x.GetCatalogueNewsOrganisationAllowlistLookup())
            .ReturnsAsync(new CatalogueNewsOrganisationAllowlistLookup { Names = [] });
        var sut = _mocker.CreateInstance<CatalogueCuratorListsProvider>();

        // Act
        var lists = await sut.GetAsync();

        // Assert
        lists.NewsOrganisationAllowlist.Should().BeEquivalentTo(CatalogueNewsOrganisationAllowlist.Names);
    }
}
