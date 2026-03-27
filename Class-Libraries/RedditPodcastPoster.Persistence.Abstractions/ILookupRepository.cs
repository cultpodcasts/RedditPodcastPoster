using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ILookupRepository
{
    Task<EliminationTerms?> GetEliminationTerms();
    Task<TKnownTerms?> GetKnownTerms<TKnownTerms>() where TKnownTerms : CosmosSelector;
    Task SaveEliminationTerms(EliminationTerms eliminationTerms);
    Task SaveKnownTerms<TKnownTerms>(TKnownTerms knownTerms) where TKnownTerms : CosmosSelector;
    Task<HomePageCache?> GetHomePageCache();
    Task SaveHomePageCache(HomePageCache homePageCache);
    Task IncrementHomePageActiveEpisodeCount(int delta);
}
