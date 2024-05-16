using RedditPodcastPoster.Discovery;

namespace Discover;

public class DiscoveryResultConsoleLogger : IDiscoveryResultConsoleLogger
{
    public void DisplayEpisode(DiscoveryResult episode, ConsoleColor defaultColor)
    {
        Console.WriteLine(new string('-', 40));
        if (episode.Url != null)
        {
            Console.WriteLine(episode.Url);
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(episode.EpisodeName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(episode.ShowName);
        if (!string.IsNullOrWhiteSpace(episode.Description))
        {
            var description = episode.Description;
            var min = Math.Min(description.Length, 200);
            Console.ForegroundColor = defaultColor;
            Console.WriteLine(description[..min]);
        }

        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine(episode.Released.ToString("g"));
        if (episode.Length != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(episode.Length);
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (var subject in episode.Subjects)
        {
            Console.WriteLine(subject);
        }

        if (episode.Views.HasValue || episode.MemberCount.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            const string unknown = "Unknown";
            Console.WriteLine(
                $"Views: {(episode.Views.HasValue ? episode.Views.Value : unknown)}, Members: {(episode.MemberCount.HasValue ? episode.MemberCount.Value : unknown)}");
        }

        Console.ForegroundColor = defaultColor;
        Console.WriteLine();
    }
}