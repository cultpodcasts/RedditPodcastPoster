using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;

namespace SeedEliminationTerms;

public class EliminationTermsSeeder
{
    private readonly IEliminationTermsRepository _eliminationTermsRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public EliminationTermsSeeder(
        IEliminationTermsRepository eliminationTermsRepository,
        ILogger<CosmosDbRepository> logger)
    {
        _eliminationTermsRepository = eliminationTermsRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var persisted = await _eliminationTermsRepository.Get();
        //persisted.Terms.Add("Add Term Here");
        persisted.Terms = persisted.Terms.Select(x => x.ToLower()).Distinct().ToList();
        await _eliminationTermsRepository.Save(persisted);
    }
}