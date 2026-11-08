using CommandLine;

namespace StoredHashtagAudit;

public class StoredHashtagAuditRequest
{
    [Option('k', "kind", Required = false, Default = "all",
        HelpText =
            "Which rows to scan: all, subject, podcast, tvshow, news, film, episode, tvshowepisode, or newsreport.")]
    public string Kind { get; set; } = "all";

    [Option('l', "list", Required = false, Default = false,
        HelpText =
            "Log each non-reproducible hashtag (id, name/title, stored, ToTagText) in addition to summary counts.")]
    public bool List { get; set; }

    [Option("sample-limit", Required = false, Default = 50,
        HelpText = "When --list is set, maximum non-reproducible rows to log per kind (0 = unlimited).")]
    public int SampleLimit { get; set; } = 50;
}
