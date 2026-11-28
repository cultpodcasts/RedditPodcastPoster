namespace Api.Models;

public class TvShowChangeRequest : ServiceIdsChange
{
    public string? ImdbId { get; set; }

    public long? TvdbId { get; set; }

    public int? TmdbId { get; set; }

    public TvShowTrailerChange[]? Trailers { get; set; }

    /// <summary>
    /// Trailer ids the client last saw. Required when <see cref="Trailers"/> is sent: a stored trailer whose id is
    /// not here is a 409, so a stale keep-list cannot delete a trailer this client never listed. Null and empty both
    /// mean the client listed none.
    /// </summary>
    public Guid[]? ExpectedTrailerIds { get; set; }

    /// <summary>
    /// Identity id changes also cascade: episodes denormalise the show's IMDb, TMDB and TVDB ids for public pages
    /// (RPP #1437).
    /// </summary>
    public override bool ChangesChildProjection =>
        base.ChangesChildProjection || ImdbId is not null || TmdbId is not null || TvdbId is not null;
}
