using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace SeedEliminationTerms;

public class EliminationTermsSeeder(
    IEliminationTermsRepository eliminationTermsRepository,
    ILogger<CosmosDbRepository> logger)
{
    public async Task Run()
    {
        var persisted = await eliminationTermsRepository.Get();
        //persisted.Terms.Add("Add Term Here");
        persisted.Terms = persisted.Terms.Select(x => x.ToLower()).Distinct().ToList();
        await eliminationTermsRepository.Save(persisted);
    }
}