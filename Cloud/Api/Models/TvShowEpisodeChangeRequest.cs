namespace Api.Models;

public class TvShowEpisodeChangeRequest : PlayableFieldsChange
{
    public string? Imdb { get; set; }

    public string? Tvdb { get; set; }
}
