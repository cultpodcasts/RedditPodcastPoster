using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.SocialPosting.Templates;

namespace Api.Services.Catalogue;

/// <summary>
/// What the curator post-status retraction hands to the tweet and Bluesky removers: the stored item projected the way
/// the social posters see it (<see cref="CatalogueParentKindMapper"/>.ToPostProjection), taken before the PATCH so the
/// match uses the release, length and AT URI the post was made with.
/// Matching is <see cref="SocialPostRemovalFingerprint"/> (#1051), the same as the hourly removers, so the
/// curator path and the post templates can't drift: TV episodes and news reports match on their site-link short id
/// or the parent line plus release line; films on their site-link short id.
/// </summary>
public sealed class SocialPostRemovalTarget
{
    private SocialPostRemovalTarget(PodcastEpisode projection)
    {
        Projection = projection;
        Fingerprint = SocialPostRemovalFingerprint.For(projection);
    }

    /// <summary>Read-only post projection. Never save it.</summary>
    public PodcastEpisode Projection { get; }

    public SocialPostRemovalFingerprint Fingerprint { get; }

    /// <summary>Item id, for logs.</summary>
    public Guid Id => Projection.Episode.Id;

    /// <summary>Stored Bluesky AT URI at the time the target was taken.</summary>
    public string? BlueskyPost => Projection.Episode.BlueskyPost;

    /// <summary>True when the post with <paramref name="text"/> (and <paramref name="linkedUrls"/>) is this item's.</summary>
    public bool Matches(string? text, IEnumerable<string?>? linkedUrls = null) =>
        Fingerprint.Matches(text, linkedUrls);

    public static SocialPostRemovalTarget For(TvShowEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        var show = new TvShow(episode.TvShowName ?? string.Empty) { Id = episode.TvShowId };
        return new SocialPostRemovalTarget(CatalogueParentKindMapper.ToPostProjection(episode, show));
    }

    public static SocialPostRemovalTarget For(NewsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var organisation = new NewsOrganisation(report.NewsOrganisationName ?? string.Empty)
        {
            Id = report.NewsOrganisationId
        };
        return new SocialPostRemovalTarget(CatalogueParentKindMapper.ToPostProjection(report, organisation));
    }

    public static SocialPostRemovalTarget For(Film film) =>
        new(CatalogueParentKindMapper.ToPostProjection(film));
}
