using CommandLine;

namespace FilmSearchPusher;

public class FilmSearchPusherRequest
{
    [Option("full", Required = false, Default = false,
        HelpText = "Push every film and delete film search documents whose film no longer exists, ignoring the saved _ts watermark.")]
    public bool Full { get; set; }

    [Option('n', "dry-run", Required = false, Default = false,
        HelpText = "Read films and report what would be pushed and deleted. No search writes, no watermark save.")]
    public bool DryRun { get; set; }

    [Option('b', "batch-size", Required = false, Default = 500,
        HelpText = "Documents per search index request (1-1000).")]
    public int BatchSize { get; set; } = 500;

    [Option("id", Required = false,
        HelpText = "Push only this film's search document (deleted if the film is removed). The watermark is not changed. Not with --full.")]
    public Guid? Id { get; set; }
}
