namespace Api.Models;

public class TvShowChangeRequest : PublisherFieldsChange
{
    public string? Imdb { get; set; }

    public string? Tvdb { get; set; }
}
