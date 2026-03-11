using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace SeedEliminationTerms;

public class EliminationTermsSeeder(
    ILookupRepositoryV2 lookupRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EliminationTermsSeeder> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run()
    {
        var persisted = await lookupRepository.GetEliminationTerms() ?? new EliminationTerms();
        //persisted.Terms.Add("Add Term Here");
        persisted.Terms = persisted.Terms.Select(x => x.ToLower()).Distinct().ToList();
        await lookupRepository.SaveEliminationTerms(persisted);
    }
}