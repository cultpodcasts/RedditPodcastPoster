using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Repositories;

public class LanguageTitleCasingRulesRepository(
    Container titleCasingRulesContainer,
    ILogger<LanguageTitleCasingRulesRepository> logger)
    : ILanguageTitleCasingRulesRepository
{
    public async Task<TitleCasingRulesDocument?> Get(string language)
    {
        var normalised = TitleCasingRulesDocument.NormaliseLanguage(language);
        if (string.IsNullOrEmpty(normalised))
        {
            return null;
        }

        var id = TitleCasingRulesDocument.IdForLanguage(normalised).ToString();
        try
        {
            return await titleCasingRulesContainer.ReadItemAsync<TitleCasingRulesDocument>(
                id,
                new PartitionKey(normalised));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<TitleCasingRulesDocument> GetAll()
    {
        var query = titleCasingRulesContainer
            .GetItemLinqQueryable<TitleCasingRulesDocument>(requestOptions: new QueryRequestOptions())
            .Where(x => x.ModelType == ModelType.LanguageTitleCasingRules);

        var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            FeedResponse<TitleCasingRulesDocument> response;
            try
            {
                response = await iterator.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Method}: error retrieving title-casing rules.", nameof(GetAll));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public async Task Save(TitleCasingRulesDocument document)
    {
        document.Language = TitleCasingRulesDocument.NormaliseLanguage(document.Language);
        document.Id = TitleCasingRulesDocument.IdForLanguage(document.Language);
        document.ModelType = ModelType.LanguageTitleCasingRules;
        await titleCasingRulesContainer.UpsertItemAsync(document, new PartitionKey(document.Language));
    }

    public async Task Delete(string language)
    {
        var normalised = TitleCasingRulesDocument.NormaliseLanguage(language);
        if (string.IsNullOrEmpty(normalised))
        {
            return;
        }

        var id = TitleCasingRulesDocument.IdForLanguage(normalised).ToString();
        try
        {
            await titleCasingRulesContainer.DeleteItemAsync<TitleCasingRulesDocument>(
                id,
                new PartitionKey(normalised));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // idempotent
        }
    }
}
