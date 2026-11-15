using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Cosmos LookUps document that decides which film an episode becomes a trailer of (#1418). Its id is
/// <see cref="IdFor"/> of the episode id (xor with <see cref="IdNamespace"/>), so it never collides with documents keyed
/// by the episode id itself. It is written with a conditional create, so of two attaches of one episode to different
/// films exactly one wins; the loser writes nothing. A claim expires on its own after <see cref="TimeToLiveSeconds"/>
/// (LookUps has container TTL enabled with defaultTtl -1).
/// </summary>
[CosmosSelector(ModelType.FilmTrailerClaim)]
public sealed class FilmTrailerClaim : CosmosSelector, IFileKeyed
{
    /// <summary>Namespace xor'd with the episode id to form this document's Cosmos id.</summary>
    public static readonly Guid IdNamespace = Guid.Parse("5d2b7c41-93e8-4f0a-b6d1-2e9a8c7f4013");

    /// <summary>A claim outlives any attach by far; after a week the episode row is long gone.</summary>
    public const int DefaultTimeToLiveSeconds = 7 * 24 * 60 * 60;

    public FilmTrailerClaim()
    {
        ModelType = ModelType.FilmTrailerClaim;
    }

    public static Guid IdFor(Guid episodeId)
    {
        var episodeBytes = episodeId.ToByteArray();
        var namespaceBytes = IdNamespace.ToByteArray();
        for (var i = 0; i < episodeBytes.Length; i++)
        {
            episodeBytes[i] ^= namespaceBytes[i];
        }

        return new Guid(episodeBytes);
    }

    [JsonPropertyName("episodeId")]
    [JsonPropertyOrder(10)]
    public Guid EpisodeId { get; set; }

    /// <summary>The film that owns the episode as a trailer.</summary>
    [JsonPropertyName("filmId")]
    [JsonPropertyOrder(11)]
    public Guid FilmId { get; set; }

    [JsonPropertyName("claimedUtc")]
    [JsonPropertyOrder(12)]
    public DateTime ClaimedUtc { get; set; }

    [JsonPropertyName("ttl")]
    [JsonPropertyOrder(13)]
    public int? TimeToLiveSeconds { get; set; } = DefaultTimeToLiveSeconds;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(FilmTrailerClaim);
}
