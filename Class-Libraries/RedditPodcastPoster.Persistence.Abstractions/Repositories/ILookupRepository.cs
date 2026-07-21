using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.HomePage;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ILookupRepository
{
    Task<EliminationTerms?> GetEliminationTerms();
    Task<DiscoveryScheduleConfig?> GetDiscoveryScheduleConfig();
    Task<TKnownTerms?> GetKnownTerms<TKnownTerms>() where TKnownTerms : CosmosSelector;
    Task SaveEliminationTerms(EliminationTerms eliminationTerms);
    Task SaveDiscoveryScheduleConfig(DiscoveryScheduleConfig config);
    Task SaveKnownTerms<TKnownTerms>(TKnownTerms knownTerms) where TKnownTerms : CosmosSelector;
    Task<HomePageCache?> GetHomePageCache();
    Task SaveHomePageCache(HomePageCache homePageCache);
    Task IncrementHomePageActiveEpisodeCount(int delta);
    Task SaveYouTubeQuotaReport(YouTubeQuotaReport report);
    Task<YouTubeQuotaReport?> GetYouTubeQuotaReport();
    Task<YouTubeIndexerKeyState?> GetYouTubeIndexerKeyState();
    Task SaveYouTubeIndexerKeyState(YouTubeIndexerKeyState state);
    Task<YouTubeQuotaUsageState?> GetYouTubeQuotaUsageState();
    Task SaveYouTubeQuotaUsageState(YouTubeQuotaUsageState state);
}
