using CommandLine;

namespace UnusableFileKeyAudit;

public class UnusableFileKeyAuditRequest
{
    [Option('k', "kind", Required = false, Default = "all",
        HelpText = "Which parents to scan: all, podcast, tvshow, news, or film.")]
    public string Kind { get; set; } = "all";

    [Option('l', "list", Required = false, Default = false,
        HelpText = "Log each unusable row (id, name, fileKey) in addition to the summary counts.")]
    public bool List { get; set; }

    [Option("sample-limit", Required = false, Default = 50,
        HelpText = "When --list is set, maximum unusable rows to log per kind (0 = unlimited).")]
    public int SampleLimit { get; set; } = 50;
}
