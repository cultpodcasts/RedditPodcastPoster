namespace Api.Models;

public class TvShowChangeRequest : ServiceIdsChange
{
    public string? Imdb { get; set; }

    public string? Tvdb { get; set; }
}
