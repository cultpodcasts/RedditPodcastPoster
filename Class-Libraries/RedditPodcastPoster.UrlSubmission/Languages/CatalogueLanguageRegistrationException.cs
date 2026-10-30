namespace RedditPodcastPoster.UrlSubmission.Languages;

/// <summary>
/// Expected curator/language-add failure (unresolvable culture or supported-languages write rejected).
/// Hosts map this to a controlled client error (BadRequest), not an InternalError.
/// </summary>
public sealed class CatalogueLanguageRegistrationException(string message) : Exception(message);
