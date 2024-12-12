using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare;

public class KVList
{
    [JsonPropertyName("result")]
    public IEnumerable<KVListResult> Result { get; set; }

}
