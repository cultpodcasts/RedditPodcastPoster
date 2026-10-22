namespace Api.Models;

public class EpisodeTrailerAttachRequest
{
    /// <summary>The film that takes the episode as a trailer.</summary>
    public Guid FilmId { get; set; }

    /// <summary>Optional trailer title. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }
}
