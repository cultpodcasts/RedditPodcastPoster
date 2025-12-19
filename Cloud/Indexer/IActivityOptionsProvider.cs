namespace Indexer;

public interface IActivityOptionsProvider
{
    bool RunIndex();
    bool RunCategoriser();
    bool RunPoster();
    bool RunPublisher();
    bool RunTweet();
    bool RunBluesky();
}