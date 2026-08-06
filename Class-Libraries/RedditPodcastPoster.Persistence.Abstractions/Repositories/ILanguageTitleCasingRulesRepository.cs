using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ILanguageTitleCasingRulesRepository
{
    Task<LanguageTitleCasingRulesDocument?> Get(string language);
    IAsyncEnumerable<LanguageTitleCasingRulesDocument> GetAll();
    Task Save(LanguageTitleCasingRulesDocument document);
    Task Delete(string language);
}
