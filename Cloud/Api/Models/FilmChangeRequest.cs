namespace Api.Models;

public class FilmChangeRequest : PublisherFieldsChange
{
    public string? Imdb { get; set; }
    public string? Description { get; set; }
    public string? Release { get; set; }
    public string? Duration { get; set; }
    public bool? Explicit { get; set; }
    public bool? Ignored { get; set; }
    public bool? Removed { get; set; }
    public string[]? Subjects { get; set; }
    public string[]? Guests { get; set; }
}
