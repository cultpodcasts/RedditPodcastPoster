using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>A news organisation or TV show that a submitted URL is routed to instead of a podcast.</summary>
public sealed record CatalogueParentMatch
{
    private CatalogueParentMatch(
        CatalogueParentKind kind,
        Publisher parent,
        CatalogueIndexingSource? indexing,
        CatalogueParentYouTubeChannels youTubeChannels)
    {
        Kind = kind;
        Parent = parent;
        Indexing = indexing;
        YouTubeChannels = youTubeChannels;
    }

    public CatalogueParentKind Kind { get; }

    public Publisher Parent { get; }

    public CatalogueIndexingSource? Indexing { get; }

    /// <summary>
    /// The YouTube channels the parent holds and the playlist that scopes it on each: the main channel, plus a news
    /// organisation's additional channels (#1370). A TV show holds its main channel only.
    /// </summary>
    public CatalogueParentYouTubeChannels YouTubeChannels { get; }

    /// <summary><see cref="SubmitClassification.NewsReport"/> or <see cref="SubmitClassification.TvShowEpisode"/>.</summary>
    public string ContentKind => Kind == CatalogueParentKind.NewsOrganisation
        ? SubmitClassification.NewsReport
        : SubmitClassification.TvShowEpisode;

    public static CatalogueParentMatch For(NewsOrganisation organisation) =>
        new(CatalogueParentKind.NewsOrganisation, organisation, organisation.Indexing,
            CatalogueParentYouTubeChannels.Of(organisation));

    public static CatalogueParentMatch For(TvShow show) =>
        new(CatalogueParentKind.TvShow, show, show.Indexing, CatalogueParentYouTubeChannels.Of(show));
}
