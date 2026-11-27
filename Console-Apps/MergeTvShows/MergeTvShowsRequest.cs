using CommandLine;

namespace MergeTvShows;

public class MergeTvShowsRequest
{
    [Option("target-id", HelpText = "Canonical TV show id that keeps the episodes (required for merge).")]
    public Guid TargetId { get; set; }

    [Option("target-name", HelpText = "Correct show name after the merge (required for merge).")]
    public string TargetName { get; set; } = "";

    [Option("source-ids", Separator = ',',
        HelpText = "Comma-separated TV show ids to empty and delete after moving their episodes (required for merge).")]
    public IEnumerable<Guid> SourceIds { get; set; } = [];

    [Option("delete-episode-ids", Separator = ',',
        HelpText = "Episode ids to hard-delete (e.g. a hub stub that is not a real episode).")]
    public IEnumerable<Guid> DeleteEpisodeIds { get; set; } = [];

    [Option("rename-episode", Separator = ',',
        HelpText = "Episode renames as id=Title (repeatable via comma list).")]
    public IEnumerable<string> RenameEpisodes { get; set; } = [];

    [Option("apply", Default = false, HelpText = "Write Cosmos + search. Default is dry-run.")]
    public bool Apply { get; set; }

    [Option("enrich-toxic-empire-meta", Default = false,
        HelpText = "One-shot #1587 meta patch (identity + air date). Ignores merge flags.")]
    public bool EnrichToxicEmpireMeta { get; set; }
}
