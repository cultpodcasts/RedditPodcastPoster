using System.Globalization;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;

namespace Api.Services.Catalogue;

/// <summary>
/// Applies the curator playable fields shared by news reports and TV-show episodes, and the parsing and
/// normalising rules films reuse. Validation runs before anything is applied, so a 400 leaves the item untouched.
/// </summary>
public static class PlayableChangeApplier
{
    public static bool TryApply(
        Playable playable,
        PlayableFieldsChange change,
        out PostStatusChange postStatus,
        out string? error)
    {
        postStatus = PostStatusChange.None;
        if (!TryParseRelease(change.Release, allowYear: false, out var release, out error) ||
            !TryParseDuration(change.Duration, out var length, out error) ||
            (change.Services is not null && !PlayableServicesChangeApplier.TryValidate(change.Services, out error)))
        {
            return false;
        }

        postStatus = PostStatusApplier.Apply(playable, change.Posted, change.Tweeted, change.UnBluesky);

        if (!string.IsNullOrWhiteSpace(change.Title))
        {
            playable.Title = change.Title.Trim();
        }

        if (change.Description != null)
        {
            playable.Description = change.Description;
        }

        if (release is not null)
        {
            playable.SetRelease(release);
        }

        if (length is not null)
        {
            playable.Length = length.Value;
        }

        if (change.Explicit != null)
        {
            playable.Explicit = change.Explicit.Value;
        }

        if (change.Ignored != null)
        {
            playable.Ignored = change.Ignored.Value;
        }

        if (change.Removed != null)
        {
            playable.Removed = change.Removed.Value;
        }

        if (change.Subjects != null)
        {
            playable.ApplyUserSubjects(change.Subjects);
        }

        if (change.SearchTerms != null)
        {
            playable.SearchTerms = NormaliseSearchTerms(change.SearchTerms);
        }

        if (change.HashTag != null)
        {
            playable.HashTag = NormaliseHashTag(change.HashTag);
        }

        if (change.Language != null)
        {
            playable.Language = EpisodeChangeApplier.NormaliseEpisodeLanguage(change.Language);
        }

        if (change.Guests != null)
        {
            playable.Guests = NormaliseGuests(change.Guests);
        }

        if (change.Services is not null)
        {
            playable.Services = PlayableServicesChangeApplier.Apply(playable.Services, change.Services);
            EpisodeServicePresence.SyncCatalogueYouTubeId(playable);
        }

        return true;
    }

    /// <summary>
    /// <c>yyyy-MM-dd</c> → a date release. With <paramref name="allowYear"/> (films), <c>yyyy</c> → a year release.
    /// Null → no change.
    /// </summary>
    public static bool TryParseRelease(string? value, bool allowYear, out CatalogueRelease? release, out string? error)
    {
        release = null;
        error = null;
        if (value == null)
        {
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var date))
        {
            release = CatalogueRelease.FromDate(date);
            return true;
        }

        if (allowYear && value.Length == 4 &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year >= 1)
        {
            release = CatalogueRelease.FromYear(year);
            return true;
        }

        error = allowYear
            ? $"Invalid release '{value}': expected a year yyyy or a calendar date yyyy-MM-dd."
            : $"Invalid release '{value}': expected a calendar date yyyy-MM-dd.";
        return false;
    }

    /// <summary>Non-negative TimeSpan, e.g. <c>0:06:30</c>. Null or blank → no change.</summary>
    public static bool TryParseDuration(string? value, out TimeSpan? length, out string? error)
    {
        length = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) || parsed < TimeSpan.Zero)
        {
            error = $"Invalid duration '{value}': expected h:mm:ss.";
            return false;
        }

        length = parsed;
        return true;
    }

    /// <summary>Same as <c>EpisodeChangeApplier</c>: replace; [] clears to null; blanks dropped; names trimmed.</summary>
    public static string[]? NormaliseGuests(string[] guests) =>
        guests.Length > 0
            ? guests.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray()
            : null;

    /// <summary>"" clears.</summary>
    public static string? NormaliseSearchTerms(string searchTerms) =>
        string.IsNullOrWhiteSpace(searchTerms) ? null : searchTerms;

    /// <summary>"" clears; trimmed.</summary>
    public static string? NormaliseHashTag(string hashTag) =>
        string.IsNullOrWhiteSpace(hashTag) ? null : hashTag.Trim();
}
