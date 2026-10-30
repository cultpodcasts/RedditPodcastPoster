namespace Api.Models;

public class TvShowEpisodeChangeRequest : PlayableFieldsChange
{
    public string? ImdbId { get; set; }

    public long? TvdbId { get; set; }

    public int? TmdbId { get; set; }
}
