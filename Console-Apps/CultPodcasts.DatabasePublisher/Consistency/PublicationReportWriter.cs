using Spectre.Console;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>Prints the publication report: what was (or would be) written per kind and what happens to existing files.</summary>
public static class PublicationReportWriter
{
    private const int ListLimit = 50;

    public static void Write(PublicationReport report)
    {
        var plan = report.Plan;
        AnsiConsole.MarkupLine(report.Applied ? "[bold]Publication summary[/]" : "[bold]Publication plan (nothing was changed)[/]");
        foreach (var kind in Enum.GetValues<PublicKind>())
        {
            AnsiConsole.MarkupLine($"  {kind}: {plan.Files.Count(f => f.Item.Kind == kind)} file(s)");
        }

        var created = plan.Files.Count(f => f.Mode == WriteMode.Create);
        var moved = plan.Moves.Count;
        AnsiConsole.MarkupLine(
            $"[bold]Output folder[/]: {plan.BaselineFileCount} existing file(s); " +
            $"{plan.Files.Count - created - moved} overwritten, {moved} moved, {plan.Deletions.Count} deleted, " +
            $"{created} created.");

        WriteList("Moved (git follows the file by its content)", plan.Moves.Select(MoveLine), "yellow");
        WriteList(
            "Deleted (explained or accepted)",
            plan.Deletions.Select(r => r.Describe() + (r.Accepted ? " [accepted]" : "")),
            "yellow");
        WriteList(
            "Created",
            plan.Files.Where(f => f.Mode == WriteMode.Create).Select(f => $"{f.Item.Kind}: '{f.Item.FileKey}.json'"),
            "green");
        WriteList(
            "UNEXPLAINED - would disappear",
            plan.Unexplained.Select(r => r.Describe()),
            "red",
            limit: int.MaxValue);
        WriteList(
            "--accept-removal values that matched no unexplained removal",
            plan.UnusedAcceptances,
            "red",
            limit: int.MaxValue);

        if (plan.Unexplained.Count > 0)
        {
            AnsiConsole.MarkupLine(
                "[red]Check each file above. To accept one, run again with --accept-removal <file key or id>.[/]");
        }

        AnsiConsole.MarkupLine(report.Applied
            ? "[green]Finished publishing public database.[/]"
            : "[red]Nothing was written, moved or deleted.[/]");
    }

    private static string MoveLine(FileRemoval removal) =>
        $"'{removal.FileKey}.json' -> '{removal.MoveTo}.json' ({removal.Reason}, id {removal.Id})";

    private static void WriteList(string heading, IEnumerable<string> lines, string colour, int limit = ListLimit)
    {
        var all = lines.ToList();
        if (all.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[{colour}]{Markup.Escape(heading)} ({all.Count}):[/]");
        foreach (var line in all.Take(limit))
        {
            AnsiConsole.MarkupLine($"  [{colour}]{Markup.Escape(line)}[/]");
        }

        if (all.Count > limit)
        {
            AnsiConsole.MarkupLine($"  [grey]... and {all.Count - limit} more[/]");
        }
    }
}
