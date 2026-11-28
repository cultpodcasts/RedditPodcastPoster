using RedditPodcastPoster.Models.Films;

namespace Api.Services.Films;

/// <summary>
/// What a conditional film create that lost (<c>TryCreate</c> false) found under the name-derived id. Shared by
/// <c>POST film</c> (<see cref="FilmCreateService"/>) and Attach as trailer so both describe the same race the same way.
/// </summary>
internal enum FilmCreateRaceKind
{
    /// <summary>Another request created the film for this name; it is the winner.</summary>
    Converged,

    /// <summary>The id's film now has a different name: it was renamed after it was created.</summary>
    Renamed,

    /// <summary>No film has the id any more: it was deleted between the failed create and the read.</summary>
    Gone
}

internal static class FilmCreateRace
{
    public const string GoneMessage = "The film changed while it was created; send again";

    public static FilmCreateRaceKind Classify(Film? existing, string filmName)
    {
        if (existing is null)
        {
            return FilmCreateRaceKind.Gone;
        }

        return string.Equals(existing.Name.Trim(), filmName, StringComparison.OrdinalIgnoreCase)
            ? FilmCreateRaceKind.Converged
            : FilmCreateRaceKind.Renamed;
    }

    public static string RenamedMessage(string filmName, string currentName) =>
        $"A film first created as '{filmName}' is now named '{currentName}'; attach to it by id or choose another name";
}
