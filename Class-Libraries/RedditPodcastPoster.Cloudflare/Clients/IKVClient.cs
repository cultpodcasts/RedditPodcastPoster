using RedditPodcastPoster.Cloudflare.Models;

namespace RedditPodcastPoster.Cloudflare.Clients;

public interface IKVClient
{
    Task<WriteResult> Write(IEnumerable<KVRecord> records, string namespaceId);
    Task<WriteResult> Write(KVRecord record, string namespaceId);
    Task<KVRecord?> ReadWithMetaData(string key, string namespaceId);
    Task<string?> Read(string key, string namespaceId);
    Task<IDictionary<string, string>> GetAll(string namespaceId);
    Task<DeleteResult> Delete(string key, string namespaceId);
    Task<DeleteResult> Delete(IEnumerable<string> keys, string namespaceId);
}
