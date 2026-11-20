using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Curator review state of an episode split off a stored episode by a submit overwrite (#1416, #1484). Null on every
/// other episode. Stored and sent as <c>pending</c> / <c>approved</c> / <c>rejected</c>.
/// </summary>
[JsonConverter(typeof(SplitOffStatusJsonConverter))]
public enum SplitOffStatus
{
    /// <summary>Just split off: hidden (<c>removed</c>) and blocked from posting until a curator reviews it.</summary>
    Pending,

    /// <summary>A curator approved it: an ordinary, public episode, posted under the usual rules.</summary>
    Approved,

    /// <summary>A curator rejected it: it stays hidden and is never posted.</summary>
    Rejected
}

/// <summary>Camel-case names only (<c>pending</c>, <c>approved</c>, <c>rejected</c>); integers are rejected.</summary>
public sealed class SplitOffStatusJsonConverter()
    : JsonStringEnumConverter<SplitOffStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
