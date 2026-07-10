using CommandLine;

namespace EpisodeGuestsLinker;

/// <summary>
/// Links Episode.Guests from matching Person twitter/bluesky handles.
/// Default is dry-run; pass --apply to surgically patch /guests only.
/// </summary>
public class EpisodeGuestsLinkRequest
{
    [Option("apply", Required = false, Default = false,
        HelpText = "Patch Cosmos episode /guests only. Without this flag, dry-run only.")]
    public bool Apply { get; set; }

    [Option("sample", Required = false, Default = 10,
        HelpText = "Max sample rows to print for episodes that would gain guests.")]
    public int Sample { get; set; } = 10;
}
