using Spectre.Console;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>Prints the publication summary: what was written per kind and what changed since the baseline.</summary>
public static class PublicationReportWriter
{
    private const int ListLimit = 50;

    public static void Write(PublicationReport report)
    {
        AnsiConsole.MarkupLine("[bold]Publication summary[/]");
        foreach (var kind in Enum.GetValues<PublicKind>())
        {
            AnsiConsole.MarkupLine($"  {kind}: {report.Published.Count(p => p.Kind == kind)} file(s)");
        }

        if (report.Comparison is not { } comparison)
        {
            AnsiConsole.MarkupLine(
                "[yellow]No --baseline supplied: files that stop being published cannot be detected.[/]");
            return;
        }

        AnsiConsole.MarkupLine(
            $"[bold]Compared with the previous publish[/] ({comparison.BaselineFileCount} file(s)): " +
            $"{comparison.Unchanged} unchanged, {comparison.Added.Count} new, " +
            $"{comparison.Removals.Count} no longer published under the same key.");

        WriteList("New files", comparison.Added.Select(a => $"{a.Kind}: '{a.FileKey}.json'"), "green");
        WriteList(
            "No longer published, explained (renamed, migrated or removed)",
            comparison.Removals.Where(r => r.Reason.IsExplained()).Select(r => r.Describe()),
            "yellow");
        WriteList(
            "No longer published, UNEXPLAINED",
            comparison.Unexplained.Select(r => r.Describe()),
            "red",
            limit: int.MaxValue);
    }

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
