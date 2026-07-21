using Indexer.Models;

namespace Indexer.Services;

public interface IIndexingStrategy
{
    bool ResolveYouTube();
    bool ExpensiveYouTubeQueries();
    bool ExpensiveSpotifyQueries();
    bool IndexSpotify();
    bool IsPrimaryPass(int pass, int totalPasses);
}