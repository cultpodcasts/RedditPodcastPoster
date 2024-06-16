using GraphQL.Client.Http;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public interface ITaddyClientFactory
{
    GraphQLHttpClient Create();
}