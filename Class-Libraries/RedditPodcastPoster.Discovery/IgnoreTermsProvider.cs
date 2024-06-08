using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Discovery;

public class IgnoreTermsProvider(IOptions<IgnoreTermsSettings> ignoreTerms, ILogger<IgnoreTermsProvider> logger)
    : IIgnoreTermsProvider
{
    private readonly IgnoreTermsSettings _ignoreTerms = ignoreTerms.Value;

    public IEnumerable<string> GetIgnoreTerms()
    {
        if (_ignoreTerms.IgnoreTerms == null)
        {
            return [];
        }

        logger.LogInformation($"{nameof(IgnoreTermsProvider)} - {nameof(GetIgnoreTerms)}: {_ignoreTerms}");
        return _ignoreTerms.IgnoreTerms.Select(x => x.ToLower());
    }
}