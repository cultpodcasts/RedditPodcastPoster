namespace Indexer;

public interface IIndexingStrategy
{
    bool ResolveYouTube();
    bool ExpensiveYouTubeQueries();
    bool ExpensiveSpotifyQueries();
}