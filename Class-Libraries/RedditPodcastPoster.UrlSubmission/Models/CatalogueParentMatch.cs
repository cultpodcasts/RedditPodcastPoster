using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>A news organisation or TV show that a submitted URL is routed to instead of a podcast.</summary>
public sealed record CatalogueParentMatch
{
    private CatalogueParentMatch(CatalogueParentKind kind, Publisher parent, CatalogueIndexingSource? indexing)
    {
        Kind = kind;
        Parent = parent;
        Indexing = indexing;
    }

    public CatalogueParentKind Kind { get; }

    public Publisher Parent { get; }

    public CatalogueIndexingSource? Indexing { get; }

    /// <summary><see cref="SubmitClassification.NewsReport"/> or <see cref="SubmitClassification.TvShowEpisode"/>.</summary>
    public string ContentKind => Kind == CatalogueParentKind.NewsOrganisation
        ? SubmitClassification.NewsReport
        : SubmitClassification.TvShowEpisode;

    public static CatalogueParentMatch For(NewsOrganisation organisation) =>
        new(CatalogueParentKind.NewsOrganisation, organisation, organisation.Indexing);

    public static CatalogueParentMatch For(TvShow show) =>
        new(CatalogueParentKind.TvShow, show, show.Indexing);
}
