using GraphQL.Client.Http;

namespace RedditPodcastPoster.PodcastServices.Taddy.Factories;

public interface ITaddyClientFactory
{
    GraphQLHttpClient Create();
}
