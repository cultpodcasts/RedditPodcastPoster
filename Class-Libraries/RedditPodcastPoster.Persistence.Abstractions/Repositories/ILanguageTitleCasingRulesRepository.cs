using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ILanguageTitleCasingRulesRepository
{
    Task<TitleCasingRulesDocument?> Get(string language);
    IAsyncEnumerable<TitleCasingRulesDocument> GetAll();
    Task Save(TitleCasingRulesDocument document);
    Task Delete(string language);
}
