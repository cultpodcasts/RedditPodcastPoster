namespace RedditPodcastPoster.UrlSubmission.Languages;

public interface ICatalogueLanguageDetector
{
    /// <summary>
    /// Detect a language from the parent and child texts together.
    /// Null code means English or not confident enough to name a language.
    /// </summary>
    CatalogueLanguageDetection Detect(CatalogueLanguageTexts texts);
}
