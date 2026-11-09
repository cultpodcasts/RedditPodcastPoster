using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

public class CatalogueCuratorListsProvider(
    ILookupRepository lookupRepository,
    ILogger<CatalogueCuratorListsProvider> logger)
    : ICatalogueCuratorListsProvider
{
    public async Task<CatalogueCuratorLists> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var allowlist = await lookupRepository.GetCatalogueNewsOrganisationAllowlistLookup();
        var stayPodcast = await lookupRepository.GetCatalogueStayPodcastNamesLookup();
        var tvShows = await lookupRepository.GetCatalogueTvShowCanonicalNamesLookup();
        var channelAliases = await lookupRepository.GetCatalogueNewsOrganisationChannelAliasesLookup();

        logger.LogDebug(
            "Catalogue curator LookUps: allowlist={Allowlist} stayPodcast={Stay} tvShows={Tv} channelAliases={Aliases}",
            allowlist is null ? "seed" : allowlist.Names.Count.ToString(),
            stayPodcast is null ? "seed" : stayPodcast.Names.Count.ToString(),
            tvShows is null ? "seed" : tvShows.ShowNameByPublisherName.Count.ToString(),
            channelAliases is null ? "seed" : channelAliases.OrganisationNameByChannelPublisherName.Count.ToString());

        return CatalogueCuratorLists.FromLookups(allowlist, stayPodcast, tvShows, channelAliases);
    }
}
