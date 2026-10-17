using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CultPodcasts.DatabasePublisher.PublicModels;
using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.Mapping;

/// <summary>
/// The <c>release</c> text of each episode (or report) in the file a previous publish left, kept so that a file
/// does not change just because of how a date-time is written. An episode that is still published with the same
/// release <i>instant</i> keeps the previous text exactly (with or without a trailing <c>Z</c>, a UTC offset, or
/// a different number of fractional digits); a different instant, an episode with no previous text, or a previous
/// text that is not a date-time takes the newly formatted value. Instants are compared as UTC, and text with no
/// zone is read as UTC, as the catalogue itself reads it. The previous file is only read: a file that is absent,
/// unreadable or not JSON gives no previous text, which is the same as no baseline.
/// </summary>
public sealed partial class BaselineReleaseText
{
    private static readonly string[] ChildArrays = ["episodes", "reports"];

    private readonly IReadOnlyDictionary<Guid, string> _byId;

    private BaselineReleaseText(IReadOnlyDictionary<Guid, string> byId) => _byId = byId;

    /// <summary>No previous text: every release keeps its new value.</summary>
    public static BaselineReleaseText None { get; } = new(new Dictionary<Guid, string>());

    public int Count => _byId.Count;

    /// <summary>
    /// Reads the release text of every episode of a podcast or TV show file (<c>episodes</c>) and every report of
    /// a news organisation file (<c>reports</c>), by child id. A child with no id or no string <c>release</c> is
    /// skipped; the first of two children with the same id wins.
    /// </summary>
    public static async Task<BaselineReleaseText> ReadAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return None;
            }

            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream);
            return FromDocument(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return None;
        }
    }

    public static BaselineReleaseText FromDocument(JsonElement root)
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

        return new BaselineReleaseText(byId);
    }

    /// <summary>
    /// The text to publish for a child: the previous text when it stands for the same instant as
    /// <paramref name="newText"/>, otherwise <paramref name="newText"/>.
    /// </summary>
    public string Choose(Guid id, string newText) =>
        _byId.TryGetValue(id, out var previous) && SameInstant(previous, newText) ? previous : newText;

    /// <summary>
    /// Both texts are dates or date-times (<c>yyyy-MM-dd</c>, optionally followed by a time and zone) and denote
    /// the same UTC instant. Anything else, a year for instance, is not the same instant.
    /// </summary>
    public static bool SameInstant(string left, string right) =>
        TryParseInstant(left, out var a) && TryParseInstant(right, out var b) && a == b;

    private static bool TryParseInstant(string text, out DateTime utc)
    {
        utc = default;
        return DateLike().IsMatch(text) &&
               DateTime.TryParse(
                   text,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                   out utc);
    }

    /// <summary>Gives every episode or report of a podcast, TV show or news organisation file its chosen release text.</summary>
    public void Apply(CosmosSelector file)
    {
        if (_byId.Count == 0)
        {
            return;
        }

        switch (file)
        {
            case PublicPodcast podcast:
                foreach (var episode in podcast.Episodes)
                {
                    episode.PreservedRelease = Choose(episode.Id, episode.ReleaseText);
                }

                break;
            case PublicTvShow show:
                ApplyToChildren(show.Episodes);
                break;
            case PublicNewsOrganisation organisation:
                ApplyToChildren(organisation.Reports);
                break;
        }
    }

    private void ApplyToChildren(IEnumerable<PublicPlayable> playables)
    {
        foreach (var playable in playables)
        {
            if (playable.Release is { } release)
            {
                playable.Release = Choose(playable.Id, release);
            }
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(T.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DateLike();
}
