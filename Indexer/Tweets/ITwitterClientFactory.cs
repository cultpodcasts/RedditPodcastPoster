using Tweetinvi;

namespace Indexer.Tweets;

public interface ITwitterClientFactory
{
    ITwitterClient Create();
}