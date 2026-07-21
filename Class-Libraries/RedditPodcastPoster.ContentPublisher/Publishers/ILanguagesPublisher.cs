namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface ILanguagesPublisher
{
    Task<bool> PublishLanguages();
}
