using RedditPodcastPoster.ContentPublisher.Publishers;

namespace PublishR2;

public class R2PublishProcessor(
    ILanguagesPublisher languagesPublisher,
    IPeoplePublisher peoplePublisher,
    ISearchSuggestionsPublisher searchSuggestionsPublisher,
    ISubjectsPublisher subjectsPublisher,
    IHomepagePublisher homepagePublisher)
{
    public async Task<bool> Process(R2PublishRequest request)
    {
        var success = true;

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
        }

        return success;
    }
}
