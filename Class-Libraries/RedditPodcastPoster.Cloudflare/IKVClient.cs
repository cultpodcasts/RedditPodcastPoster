using RedditPodcastPoster.Configuration;

namespace RedditPodcastPoster.Cloudflare;

public interface IKVClient
{
    Task<WriteResult> Write(IEnumerable<KVRecord> records, Func<CloudFlareOptions, string> selector);
    Task<WriteResult> Write(KVRecord record, Func<CloudFlareOptions, string> selector);
    Task<KVRecord?> Read(string key, Func<CloudFlareOptions, string> selector);
    Task<IEnumerable<KVRecord>?> GetAll(Func<CloudFlareOptions, string> selector);
}
