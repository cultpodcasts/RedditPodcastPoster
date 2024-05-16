using RedditPodcastPoster.Discovery;

namespace Discover;

public interface IDiscoveryResultConsoleLogger
{
    void DisplayEpisode(DiscoveryResult episode, ConsoleColor defaultColor);
}