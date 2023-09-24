namespace Indexer.Tweets;

public interface ITwitterClient
{
    Task<bool> Send(string tweet);
}