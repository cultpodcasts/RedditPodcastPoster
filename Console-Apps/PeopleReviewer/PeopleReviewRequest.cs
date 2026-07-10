using CommandLine;

namespace PeopleReviewer;

/// <summary>
/// Local People seed JSON reviewer. Edits JSON on disk only — never writes Cosmos or episodes.
/// </summary>
public class PeopleReviewRequest
{
    [Option("seed-path", Required = false,
        HelpText = "Path to people-seed JSON to load and save (default: sample-people-seed.json next to the app).")]
    public string? SeedPath { get; set; }

    [Option("port", Required = false, Default = 5188,
        HelpText = "HTTP port for the review UI.")]
    public int Port { get; set; }
}
