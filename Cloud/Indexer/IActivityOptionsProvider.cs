namespace Indexer;

public interface IActivityOptionsProvider
{
    bool RunIndex(out string reason);
    bool RunCategoriser(out string reason);
    bool RunPoster(out string reason);
    bool RunPublisher(out string reason);
    bool RunTweet(out string reason);
    bool RunBluesky(out string reason);
}