namespace Api.Models;

public class EpisodeFilmMoveRequest
{
    /// <summary>Optional film name. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }
}
