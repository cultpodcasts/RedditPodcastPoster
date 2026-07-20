using RedditPodcastPoster.ContentPublisher;

namespace PublishR2;

public class R2PublishProcessor(
    ILanguagesPublisher languagesPublisher,
    IPeoplePublisher peoplePublisher)
{
    public async Task<bool> Process(R2PublishRequest request)
    {
        var success = true;

        if (request.Target is R2PublishTarget.Languages or R2PublishTarget.All)
        {
            success = await languagesPublisher.PublishLanguages();
        }

        if (success && request.Target is R2PublishTarget.People or R2PublishTarget.All)
        {
            await peoplePublisher.PublishPeople();
        }

        return success;
    }
}
