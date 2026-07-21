using RedditPodcastPoster.ContentPublisher.Publishers;

namespace PublishR2;

public class FlairPublishProcessor(ISubjectsPublisher subjectsPublisher)
{
    public async Task Process(FlairPublishRequest request)
    {
        _ = request;
        await subjectsPublisher.PublishFlairs();
    }
}
