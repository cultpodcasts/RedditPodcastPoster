namespace Api.Models;

public class TvShowChangeRequest : ServiceIdsChange
{
    public string? ImdbId { get; set; }

    public long? TvdbId { get; set; }

    public int? TmdbId { get; set; }

    /// <summary>
    /// Identity id changes also cascade: episodes denormalise the show's IMDb, TMDB and TVDB ids for public pages
    /// (RPP #1437).
    /// </summary>
    public override bool ChangesChildProjection =>
        base.ChangesChildProjection || ImdbId is not null || TmdbId is not null || TvdbId is not null;
}
