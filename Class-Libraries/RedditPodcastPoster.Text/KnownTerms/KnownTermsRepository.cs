using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Text.KnownTerms;

public class KnownTermsRepository(
    IDataRepository dataRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<KnownTermsRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IKnownTermsRepository
{
    public async Task<KnownTerms> Get()
    {
        return (await dataRepository.Read<KnownTerms>(KnownTerms._Id.ToString()))!;
    }

    public Task Save(KnownTerms terms)
    {
        return dataRepository.Write(terms);
    }
}