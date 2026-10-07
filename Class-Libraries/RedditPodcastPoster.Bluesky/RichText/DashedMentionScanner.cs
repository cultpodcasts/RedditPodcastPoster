using System.Text;
using System.Text.RegularExpressions;
using idunno.AtProto;

namespace RedditPodcastPoster.Bluesky.RichText;

public readonly record struct DashedMention(string Handle, long ByteStart, long ByteEnd);

public static class DashedMentionScanner
{
    // Same leading boundary as idunno's default mention regex. Segments allow internal hyphens,
    // require a dot, and the lookahead keeps this pass to handles the default `\w` pattern misses.
    private static readonly Regex DashedMentionPattern = new(
        @"(?:^|\s|\()(@(?=[A-Za-z0-9.-]*-)(?:[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*)(?:\.(?:[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*))+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<DashedMention> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var matches = DashedMentionPattern.Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var found = new List<DashedMention>(matches.Count);
        foreach (Match match in matches)
        {
            var token = match.Groups[1];
            var handle = token.Value[1..];
            if (!handle.Contains('-') || !Handle.TryParse(handle, out _))
            {
                continue;
            }

            var byteStart = Utf8BytePosition(text, token.Index);
            var byteEnd = Utf8BytePosition(text, token.Index + token.Length);
            found.Add(new DashedMention(handle, byteStart, byteEnd));
        }

        return found;
    }

    private static long Utf8BytePosition(string text, int charIndex) =>
        Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));
}
