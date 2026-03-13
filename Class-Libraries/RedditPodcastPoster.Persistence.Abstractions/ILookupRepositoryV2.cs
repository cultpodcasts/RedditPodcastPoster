using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ILookupRepositoryV2
{
    Task<EliminationTerms?> GetEliminationTerms();
    Task<TKnownTerms?> GetKnownTerms<TKnownTerms>() where TKnownTerms : CosmosSelector;
    Task SaveEliminationTerms(EliminationTerms eliminationTerms);
    Task SaveKnownTerms<TKnownTerms>(TKnownTerms knownTerms) where TKnownTerms : CosmosSelector;
}
