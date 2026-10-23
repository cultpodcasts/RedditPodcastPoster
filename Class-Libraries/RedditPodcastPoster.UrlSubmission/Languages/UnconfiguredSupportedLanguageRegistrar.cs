namespace RedditPodcastPoster.UrlSubmission.Languages;

/// <summary>
/// Placeholder so hosts that never add a language can still construct the gate.
/// The API replaces this with the supported-languages update service.
/// </summary>
public sealed class UnconfiguredSupportedLanguageRegistrar : ISupportedLanguageRegistrar
{
    public Task RegisterAsync(string englishName, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Adding a supported language is only available from the API supported-languages update.");
}
