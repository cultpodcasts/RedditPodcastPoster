using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace SeedCatalogueCuratorLookups;

/// <summary>
/// Seeds the four catalogue curator LookUps documents from compiled static sets.
/// Dry-run by default. Agents must never pass --apply against production.
/// </summary>
public class SeedCatalogueCuratorLookupsProcessor(
    ILookupRepository lookupRepository,
    ILogger<SeedCatalogueCuratorLookupsProcessor> logger)
{
    public async Task<int> Run(SeedCatalogueCuratorLookupsRequest request)
    {
        var allowlistSeed = CatalogueNewsOrganisationAllowlistLookup.CreateFromSeed();
        var staySeed = CatalogueStayPodcastNamesLookup.CreateFromSeed();
        var tvSeed = CatalogueTvShowCanonicalNamesLookup.CreateFromSeed();
        var aliasesSeed = CatalogueNewsOrganisationChannelAliasesLookup.CreateFromSeed();

        var existingAllowlist = await lookupRepository.GetCatalogueNewsOrganisationAllowlistLookup();
        var existingStay = await lookupRepository.GetCatalogueStayPodcastNamesLookup();
        var existingTv = await lookupRepository.GetCatalogueTvShowCanonicalNamesLookup();
        var existingAliases = await lookupRepository.GetCatalogueNewsOrganisationChannelAliasesLookup();

        logger.LogInformation(
            "Would seed catalogue curator LookUps. Apply={Apply} Force={Force}. " +
            "Allowlist seed={AllowlistSeed} existing={AllowlistExisting}. " +
            "StayPodcast seed={StaySeed} existing={StayExisting}. " +
            "TvCanonical seed={TvSeed} existing={TvExisting}. " +
            "ChannelAliases seed={AliasesSeed} existing={AliasesExisting}.",
            request.Apply,
            request.Force,
            allowlistSeed.Names.Count,
            existingAllowlist?.Names.Count,
            staySeed.Names.Count,
            existingStay?.Names.Count,
            tvSeed.ShowNameByPublisherName.Count,
            existingTv?.ShowNameByPublisherName.Count,
            aliasesSeed.OrganisationNameByChannelPublisherName.Count,
            existingAliases?.OrganisationNameByChannelPublisherName.Count);

        if (!request.Apply)
        {
            logger.LogInformation("Dry-run only. Pass --apply to write (human curator action).");
            return 0;
        }

        await WriteIfNeeded(
            "CatalogueNewsOrganisationAllowlistLookup",
            existingAllowlist is not null,
            request.Force,
            () => lookupRepository.SaveCatalogueNewsOrganisationAllowlistLookup(allowlistSeed));
        await WriteIfNeeded(
            "CatalogueStayPodcastNamesLookup",
            existingStay is not null,
            request.Force,
            () => lookupRepository.SaveCatalogueStayPodcastNamesLookup(staySeed));
        await WriteIfNeeded(
            "CatalogueTvShowCanonicalNamesLookup",
            existingTv is not null,
            request.Force,
            () => lookupRepository.SaveCatalogueTvShowCanonicalNamesLookup(tvSeed));
        await WriteIfNeeded(
            "CatalogueNewsOrganisationChannelAliasesLookup",
            existingAliases is not null,
            request.Force,
            () => lookupRepository.SaveCatalogueNewsOrganisationChannelAliasesLookup(aliasesSeed));

        return 0;
    }

    private async Task WriteIfNeeded(string name, bool exists, bool force, Func<Task> write)
    {
        if (exists && !force)
        {
            logger.LogInformation(
                "{Name} already exists — skipping write (pass --force to overwrite).",
                name);
            return;
        }

        await write();
        logger.LogInformation("Saved {Name}.", name);
    }
}
