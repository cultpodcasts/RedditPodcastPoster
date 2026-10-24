namespace Api.Models;

public class TvShowChangeRequest : ServiceIdsChange
{
    public string? ImdbId { get; set; }

    public long? TvdbId { get; set; }

    public int? TmdbId { get; set; }
}
