using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Text.TitleCasing;

/// <summary>Precompiled known-term replacement for the sanitise hot path.</summary>
public readonly record struct KnownTermReplacement(Regex Pattern, string Literal);
