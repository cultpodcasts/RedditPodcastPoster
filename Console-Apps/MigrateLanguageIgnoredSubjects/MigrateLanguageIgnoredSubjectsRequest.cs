using CommandLine;

namespace MigrateLanguageIgnoredSubjects;

public class MigrateLanguageIgnoredSubjectsRequest
{
    [Option("apply-seed", HelpText = "Phase 1: write language-level IgnoredSubjects on TitleCasingRules (and write audit JSON).")]
    public bool ApplySeed { get; set; }

    [Option("apply-clear", HelpText = "Phase 2: strip migrated names from non-English podcast IgnoredSubjects using the audit file.")]
    public bool ApplyClear { get; set; }

    [Option("audit-path", HelpText = "Path for the machine-readable audit/undo JSON (default: ./language-ignored-subjects-migration-audit.json).")]
    public string AuditPath { get; set; } = "language-ignored-subjects-migration-audit.json";
}
