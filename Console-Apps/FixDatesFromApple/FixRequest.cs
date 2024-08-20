using CommandLine;

namespace FixDatesFromApple;

public class FixRequest
{
    [Value(0, Required = true, HelpText = "The id of the podcast to index")]
    public Guid PodcastId { get; set; }

    [Value(1, Required = true, HelpText = "The incorrect date to fix")]
    public DateOnly Date { get; set; }
}