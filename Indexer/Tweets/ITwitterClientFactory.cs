using Amazon.Runtime.Internal.Util;
using Azure.Core;

namespace Indexer.Tweets;

public interface ITwitterClientFactory
{
    TwitterClient Create();
}