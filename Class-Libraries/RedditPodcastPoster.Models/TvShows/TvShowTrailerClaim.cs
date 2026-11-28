using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

/// <summary>
/// Cosmos LookUps document that decides which TV show an episode becomes a trailer of (RPP #1412), as
/// <see cref="Films.FilmTrailerClaim"/> does for films. Its id is <see cref="IdFor"/> of the episode id (xor with
/// <see cref="IdNamespace"/>), so it never collides with documents keyed by the episode id itself or with a film
/// claim. It is written with a conditional create, so of two attaches of one episode to different shows exactly one
/// wins; the loser writes nothing. A claim expires on its own after <see cref="DefaultTimeToLiveSeconds"/> (LookUps
/// has container TTL enabled with defaultTtl -1).
/// </summary>
[CosmosSelector(ModelType.TvShowTrailerClaim)]
public sealed class TvShowTrailerClaim : CosmosSelector, IFileKeyed
{
    /// <summary>Namespace xor'd with the episode id to form this document's Cosmos id.</summary>
    public static readonly Guid IdNamespace = Guid.Parse("bba12e1f-b09f-4e39-8cb0-9a1f5eb8784e");

    /// <summary>A claim outlives any attach by far; after a week the episode row is long gone.</summary>
    public const int DefaultTimeToLiveSeconds = 7 * 24 * 60 * 60;

    public TvShowTrailerClaim()
    {
        ModelType = ModelType.TvShowTrailerClaim;
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

    /// <summary>The TV show that owns the episode as a trailer.</summary>
    [JsonPropertyName("tvShowId")]
    [JsonPropertyOrder(11)]
    public Guid TvShowId { get; set; }

    [JsonPropertyName("claimedUtc")]
    [JsonPropertyOrder(12)]
    public DateTime ClaimedUtc { get; set; }

    [JsonPropertyName("ttl")]
    [JsonPropertyOrder(13)]
    public int? TimeToLiveSeconds { get; set; } = DefaultTimeToLiveSeconds;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(TvShowTrailerClaim);
}
