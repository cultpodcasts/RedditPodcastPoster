namespace Api.Models;

/// <summary>
/// Target of an attach of an episode to a TV show as a trailer (RPP #1412). Exactly one of <see cref="TvShowId"/> or
/// <see cref="TvShowName"/> is given, as for a move to a TV show.
/// </summary>
public class EpisodeTvShowTrailerAttachRequest
{
    /// <summary>An existing TV show, by id.</summary>
    public Guid? TvShowId { get; set; }

    /// <summary>A TV show by name (case-insensitive). With <see cref="CreateShow"/> a missing show is created.</summary>
    public string? TvShowName { get; set; }

    /// <summary>
    /// With <see cref="TvShowName"/>: create the show when no show has the name (a show with no episodes, no indexing).
    /// When one show already has it, the trailer joins that show and <see cref="Publisher"/> is not applied.
    /// </summary>
    public bool CreateShow { get; set; }

    /// <summary>Publisher of a newly created show. Only valid with <see cref="CreateShow"/>.</summary>
    public string? Publisher { get; set; }

    /// <summary>Optional trailer title. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }

    /// <summary>The same target as a Move to TV show request, for the validation the two share (<c>TvShowMoveTarget</c>).</summary>
    public EpisodeTvShowMoveRequest ToTarget() => new()
    {
        TvShowId = TvShowId,
        TvShowName = TvShowName,
        CreateShow = CreateShow,
        Publisher = Publisher,
        Title = Title
    };
}

