namespace Indexer.Tweets;

public interface ITweeter
{
    Task Tweet(bool youTubeRefreshed, bool spotifyRefreshed);
}