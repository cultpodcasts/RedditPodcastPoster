using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RedditPodcastPoster.Bluesky.Client;

public readonly record struct MentionSpan(int CharIndex, int CharLength, string HandleWithAt);

/// <summary>
/// X.Bluesky 2.0.7 mention detection is still <c>@\w+(\.\w+)*</c>. A dash truncates the handle,
/// DID lookup throws, and the post never reaches createRecord. Mask those mentions before
/// <c>Post</c>, then restore them on the createRecord body and attach the resolved facets.
/// </summary>
public static class CreateRecordMentionPatch
{
    internal static readonly Regex MentionRegex = new(
        @"(?<![\w@.-])@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+",
        RegexOptions.Compiled);

    public static (string MaskedText, IReadOnlyList<MentionSpan> Mentions) Mask(string text)
    {
        var matches = MentionRegex.Matches(text);
        if (matches.Count == 0)
        {
            return (text, []);
        }

        var chars = text.ToCharArray();
        var mentions = new List<MentionSpan>(matches.Count);
        foreach (Match match in matches)
        {
            chars[match.Index] = '~';
            mentions.Add(new MentionSpan(match.Index, match.Length, match.Value));
        }

        return (new string(chars), mentions);
    }

    public static string Restore(
        string createRecordJson,
        IReadOnlyList<MentionSpan> mentions,
        IReadOnlyDictionary<string, string> didByHandle)
    {
        var root = JObject.Parse(createRecordJson);
        if (root["record"] is not JObject record || record["text"]?.ToString() is not { } text)
        {
            return createRecordJson;
        }

        var chars = text.ToCharArray();
        foreach (var mention in mentions)
        {
            if ((uint)mention.CharIndex < (uint)chars.Length && chars[mention.CharIndex] == '~')
            {
                chars[mention.CharIndex] = '@';
            }
        }

        record["text"] = new string(chars);
        var facets = record["facets"] as JArray ?? [];
        foreach (var mention in mentions)
        {
            if (!didByHandle.TryGetValue(mention.HandleWithAt, out var did) || string.IsNullOrWhiteSpace(did))
            {
                continue;
            }

            if ((uint)mention.CharIndex >= (uint)text.Length)
            {
                continue;
            }

            var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, mention.CharIndex));
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(text.AsSpan(mention.CharIndex, mention.CharLength));
            facets.Add(new JObject
            {
                ["index"] = new JObject
                {
                    ["byteStart"] = byteStart,
                    ["byteEnd"] = byteEnd
                },
                ["features"] = new JArray
                {
                    new JObject
                    {
                        ["$type"] = "app.bsky.richtext.facet#mention",
                        ["did"] = did
                    }
                }
            });
        }

        record["facets"] = facets;
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string? ReadCreatedUri(string responseJson)
    {
        try
        {
            var uri = JObject.Parse(responseJson)["uri"]?.ToString();
            return string.IsNullOrWhiteSpace(uri) ? null : uri;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}
