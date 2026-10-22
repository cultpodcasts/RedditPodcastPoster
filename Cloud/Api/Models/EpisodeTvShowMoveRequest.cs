namespace Api.Models;

/// <summary>
/// Target of a move to a TV show. Exactly one of <see cref="TvShowId"/> or <see cref="TvShowName"/> is given.
/// </summary>
public class EpisodeTvShowMoveRequest
{
    /// <summary>An existing TV show, by id.</summary>
    public Guid? TvShowId { get; set; }

    /// <summary>A TV show by name (case-insensitive). With <see cref="CreateShow"/> a missing show is created.</summary>
    public string? TvShowName { get; set; }

    /// <summary>
    /// With <see cref="TvShowName"/>: create the show when no show has the name. When one show already has it, the
    /// episode joins that show and <see cref="Publisher"/> is not applied.
    /// </summary>
    public bool CreateShow { get; set; }

    /// <summary>Publisher of a newly created show. Only valid with <see cref="CreateShow"/>.</summary>
    public string? Publisher { get; set; }

    /// <summary>Optional TV episode title. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }
}
