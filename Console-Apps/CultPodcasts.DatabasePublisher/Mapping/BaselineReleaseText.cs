using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CultPodcasts.DatabasePublisher.Mapping;

/// <summary>
/// The <c>release</c> text of each episode (or report) in the file a previous publish left, kept so that a file
/// does not change just because of how a date-time is written. A child that is still published with the same
/// release <i>instant</i> keeps the previous text exactly (with or without a trailing <c>Z</c>, a UTC offset, or
/// a different number of fractional digits). A different instant, a child with no previous text, or a previous
/// text that is not a full date-time (a year or a bare date for instance, which have a coarser precision than a
/// date-time and so are never held against one) takes the newly formatted value. Instants are compared as UTC,
/// and text with no zone is read as UTC, as the catalogue itself reads it. The texts are read once, with the
/// baseline file they belong to (<see cref="Consistency.BaselineFile.Releases"/>), and are never written back.
/// </summary>
public sealed partial class BaselineReleaseText
{
    private static readonly string[] ChildArrays = ["episodes", "reports"];

    private readonly IReadOnlyDictionary<Guid, string> _byId;

    private BaselineReleaseText(IReadOnlyDictionary<Guid, string> byId) => _byId = byId;

    /// <summary>No previous text: every release keeps its new value.</summary>
    public static BaselineReleaseText None { get; } = new(new Dictionary<Guid, string>());

    /// <summary>
    /// Reads the release text of every episode of a podcast or TV show file (<c>episodes</c>) and every report of
    /// a news organisation file (<c>reports</c>), by child id. A child with no id or no string <c>release</c> is
    /// skipped; the first of two children with the same id wins.
    /// </summary>
    internal static BaselineReleaseText FromDocument(JsonElement root)
    {
        var byId = new Dictionary<Guid, string>();
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in ChildArrays)
            {
                if (!root.TryGetProperty(name, out var children) || children.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var child in children.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.Object &&
                        child.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                        id.TryGetGuid(out var guid) &&
                        child.TryGetProperty("release", out var release) && release.ValueKind == JsonValueKind.String &&
                        release.GetString() is { } text)
                    {
                        byId.TryAdd(guid, text);
                    }
                }
            }
        }

        return byId.Count == 0 ? None : new BaselineReleaseText(byId);
    }

    /// <summary>
    /// The text to publish for a child: the previous text when it stands for the same instant as
    /// <paramref name="newText"/>, otherwise <paramref name="newText"/>.
    /// </summary>
    [return: NotNullIfNotNull(nameof(newText))]
    public string? Choose(Guid id, string? newText) =>
        newText is not null && _byId.TryGetValue(id, out var previous) && SameInstant(previous, newText)
            ? previous
            : newText;

    /// <summary>
    /// Both texts are full date-times (<c>yyyy-MM-ddTHH:mm:ss</c>, optionally with up to seven fractional digits
    /// and a <c>Z</c> or <c>+hh:mm</c> offset) and denote the same UTC instant. Anything else, a year or a bare
    /// date for instance, is not the same instant.
    /// </summary>
    internal static bool SameInstant(string left, string right) =>
        TryParseInstant(left, out var a) && TryParseInstant(right, out var b) && a == b;

    private static bool TryParseInstant(string text, out DateTime utc)
    {
        utc = default;
        return DateTimeShape().IsMatch(text) &&
               DateTime.TryParse(
                   text,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out utc);
    }

    [GeneratedRegex(
        @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})?\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex DateTimeShape();
}