using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Text.KnownTerms;

namespace SeedKnownTerms;

public class KnownTermsSeeder(
    IKnownTermsRepository knownTermsRepository,
    ILogger<CosmosDbRepository> logger)
{
    private readonly Dictionary<string, Regex> _knownTerms = new()
    {
        {"JWs", new Regex(@"\bJWs\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"ABCs", new Regex(@"\bABCs\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"ExJWHelp", new Regex(@"\bExJWHelp\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"PBCC", new Regex(@"\bPBCC\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"BJU", new Regex(@"\bBJU\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"JW", new Regex(@"\bJW\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"IBLP", new Regex(@"\bIBLP\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"EDUCO", new Regex(@"\bEDUCO\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)},
        {"NXIVM", new Regex(@"\bNXIVM\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)}
    };

    public async Task Run()
    {
        var persisted = new KnownTerms();
        foreach (var knownTerm in _knownTerms)
        {
            persisted.Terms.Add(knownTerm.Key, knownTerm.Value);
        }

        await knownTermsRepository.Save(persisted);

        persisted = await knownTermsRepository.Get();
    }
}