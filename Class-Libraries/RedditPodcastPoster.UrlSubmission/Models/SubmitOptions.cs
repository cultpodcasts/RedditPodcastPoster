using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Languages;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <param name="RefreshMeta">
/// Force streaming page meta extract even if the URL is already known (SubmitUrl <c>-r</c>).
/// Overwrite vs fill-missing enrichment is chosen at the composition root by which
/// <c>IEpisodeEnricher</c> is registered — not by this flag.
/// </param>
/// <param name="Parent">
/// File the URL under this news organisation / TV show (by id) and never create a podcast. Exclusive with
/// <paramref name="PodcastId"/> / <paramref name="PodcastName"/>.
/// </param>
/// <param name="EnrichOnly">
/// News organisation / TV show submits only: the URL may enrich (or match) a stored news report / TV episode but never
/// creates one. Discovery sets it for a result's extra URLs once its item exists, so one discovered episode is one item.
/// </param>
public record SubmitOptions(
    Guid? PodcastId,
    bool MatchOtherServices,
    bool PersistToDatabase = true,
    bool CreatePodcast = false,
    string? PodcastName = null,
    EpisodeCreationSource CreationSource = EpisodeCreationSource.SubmitUrl,
    NonPodcastServiceItemMetaData? PrefetchedMeta = null,
    bool RefreshMeta = false,
    SubmitClassificationSignals? ClassificationSignals = null,
    CatalogueParentTarget? Parent = null,
    bool EnrichOnly = false,
    bool EnforceLanguageGate = false,
    IReadOnlyCollection<string>? SupportedLanguageCodes = null,
    CatalogueLanguageChoice LanguageChoice = CatalogueLanguageChoice.Unset,
    bool LanguageProbe = false,
    CatalogueIdentityChoice IdentityChoice = CatalogueIdentityChoice.Unset,
    string? SelectedIdentityKey = null);
