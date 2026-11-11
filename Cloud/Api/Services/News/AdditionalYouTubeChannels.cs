using System.Text.RegularExpressions;
using Api.Models;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.News;

/// <summary>A news organisation PATCH whose <c>additionalYouTubeChannelIds</c> cannot be stored (400).</summary>
public sealed record AdditionalYouTubeChannelsRefusal(string Code, string Message);

/// <summary>
/// The news organisation PATCH rules for <c>indexing.additionalYouTubeChannelIds</c> (#1370): an outlet with more than
/// one YouTube channel (for example a main channel and an archive channel) indexes and owns each of them as a whole
/// channel. A list replaces the stored list and <c>[]</c> clears it; values are trimmed and blanks and repeats dropped
/// (<see cref="CatalogueIndexingSource.AdditionalYouTubeChannelIds"/>). Each value must look like a YouTube channel id
/// (<c>UC</c> + 22 characters), so a handle or URL pasted by mistake is refused instead of silently indexing nothing,
/// and none may be the organisation's main channel after the PATCH.
/// </summary>
public static partial class AdditionalYouTubeChannels
{
    public const string InvalidChannelIdCode = "invalidAdditionalYouTubeChannelId";

    public const string MainChannelCode = "additionalYouTubeChannelIsMainChannel";

    /// <summary>True when the PATCH touches the main or the additional channels, so the rules must be checked.</summary>
    public static bool ChangeAffectsChannels(NewsOrganisationChangeRequest change) =>
        change.AdditionalYouTubeChannelIds != null || change.YouTubeChannelId != null;

    /// <summary>The additional channels the organisation will hold after the PATCH.</summary>
    public static IReadOnlyList<string> Projected(CatalogueIndexingSource? indexing, NewsOrganisationChangeRequest change)
    {
        if (change.AdditionalYouTubeChannelIds is { } requested)
        {
            return new CatalogueIndexingSource { AdditionalYouTubeChannelIds = [.. requested] }
                .AdditionalYouTubeChannelIds ?? [];
        }

        return indexing?.AdditionalYouTubeChannelIds ?? [];
    }

    /// <summary>Null when the projected channels may be stored; otherwise the 400 refusal.</summary>
    public static AdditionalYouTubeChannelsRefusal? Check(
        CatalogueIndexingSource? indexing,
        NewsOrganisationChangeRequest change)
    {
        if (!ChangeAffectsChannels(change))
        {
            return null;
        }

        var additional = Projected(indexing, change);
        if (change.AdditionalYouTubeChannelIds != null &&
            additional.FirstOrDefault(id => !ChannelIdPattern().IsMatch(id)) is { } invalid)
        {
            return new AdditionalYouTubeChannelsRefusal(
                InvalidChannelIdCode,
                $"'{invalid}' is not a YouTube channel id (UC followed by 22 characters).");
        }

        var main = change.YouTubeChannelId != null
            ? (string.IsNullOrWhiteSpace(change.YouTubeChannelId) ? null : change.YouTubeChannelId.Trim())
            : indexing?.YouTubeChannelId;
        if (main != null && additional.Contains(main, StringComparer.Ordinal))
        {
            return new AdditionalYouTubeChannelsRefusal(
                MainChannelCode,
                $"YouTube channel '{main}' is the main youTubeChannelId; it cannot also be an additional channel.");
        }

        return null;
    }

    /// <summary>
    /// Stores the requested list on <paramref name="indexing"/> (created only when a channel is actually set). Returns
    /// the block to keep; unchanged when the PATCH leaves the list alone.
    /// </summary>
    public static CatalogueIndexingSource? Apply(CatalogueIndexingSource? indexing, NewsOrganisationChangeRequest change)
    {
        if (change.AdditionalYouTubeChannelIds is not { } requested)
        {
            return indexing;
        }

        if (indexing == null)
        {
            var created = new CatalogueIndexingSource { AdditionalYouTubeChannelIds = [.. requested] };
            return created.AdditionalYouTubeChannelIds == null ? null : created;
        }

        indexing.AdditionalYouTubeChannelIds = [.. requested];
        return indexing;
    }

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChannelIdPattern();
}
