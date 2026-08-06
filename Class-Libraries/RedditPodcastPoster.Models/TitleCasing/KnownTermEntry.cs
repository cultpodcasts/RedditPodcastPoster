using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models.TitleCasing;

/// <summary>
/// Match <see cref="Pattern"/> and replace with <see cref="Literal"/> (literal may include spaces).
/// </summary>
public sealed class KnownTermEntry
{
    [JsonPropertyName("literal")]
    public required string Literal { get; set; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; set; }

    /// <summary>Comma-separated <see cref="RegexOptions"/> names, e.g. <c>IgnoreCase, Compiled</c>.</summary>
    [JsonPropertyName("options")]
    public string Options { get; set; } = nameof(RegexOptions.IgnoreCase) + ", " + nameof(RegexOptions.Compiled);

    public Regex ToRegex()
    {
        var parsed = string.IsNullOrWhiteSpace(Options)
            ? RegexOptions.IgnoreCase | RegexOptions.Compiled
            : (RegexOptions)Enum.Parse(typeof(RegexOptions), Options.Replace(" ", ""), ignoreCase: true);
        return new Regex(Pattern, parsed);
    }
}
