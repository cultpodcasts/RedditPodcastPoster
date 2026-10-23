namespace RedditPodcastPoster.UrlSubmission.Languages;

/// <summary>
/// Adds one language to the product supported-languages list (the same write the admin dialog uses).
/// </summary>
public interface ISupportedLanguageRegistrar
{
    Task RegisterAsync(string englishName, CancellationToken cancellationToken = default);
}
