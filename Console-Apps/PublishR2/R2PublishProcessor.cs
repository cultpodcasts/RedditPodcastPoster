using RedditPodcastPoster.ContentPublisher.ApiCache;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace PublishR2;

public class R2PublishProcessor(
    ILanguagesPublisher languagesPublisher,
    IPeoplePublisher peoplePublisher,
    ISearchSuggestionsPublisher searchSuggestionsPublisher,
    ISubjectsPublisher subjectsPublisher,
    IHomepagePublisher homepagePublisher,
    IApiCachePurgeCommandBuilder apiCachePurgeCommandBuilder)
{
    public async Task<R2PublishResult> Process(R2PublishRequest request)
    {
        var success = true;
        var published = new List<ApiCachedContent>();

        if (request.Target is R2PublishTarget.Languages or R2PublishTarget.Lookups or R2PublishTarget.All)
        {
            success = await languagesPublisher.PublishLanguages();
        }

        if (success && request.Target is R2PublishTarget.People or R2PublishTarget.Lookups or R2PublishTarget.All)
        {
            await peoplePublisher.PublishPeople();
        }

        if (success && request.Target is R2PublishTarget.SearchSuggestions or R2PublishTarget.Lookups or R2PublishTarget.All)
        {
            try
            {
                await searchSuggestionsPublisher.PublishSearchSuggestions();
                published.Add(ApiCachedContent.SearchSuggestions);
            }
            catch
            {
                // Publisher already logged the exception; map to CLI failure.
                success = false;
            }
        }

        if (success && request.Target is R2PublishTarget.Subjects or R2PublishTarget.Lookups)
        {
            await subjectsPublisher.PublishSubjects();
        }

        if (success && request.Target is R2PublishTarget.Homepage or R2PublishTarget.All)
        {
            var homepageResult = await homepagePublisher.PublishHomepage();
            success = homepageResult.HomepagePublished;
            if (homepageResult.HomepagePublished)
            {
                published.Add(ApiCachedContent.Homepage);
            }
        }

        return new R2PublishResult(success, apiCachePurgeCommandBuilder.Build(published));
    }
}
