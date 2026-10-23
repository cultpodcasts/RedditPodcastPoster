using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Languages;

public interface ICatalogueLanguageGate
{
    Task<CatalogueLanguageAdmission> Evaluate(
        CatalogueLanguageTexts texts,
        bool creatingNewParent,
        SubmitOptions options,
        CancellationToken cancellationToken = default);
}
