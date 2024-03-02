using CommandLine;

namespace SubjectSeeder;

public class SubjectRequest
{
    [Value(0, MetaName = "subject-name", HelpText = "The name of the subject", Required = true)]
    public string Name { get; set; }

    [Option('a', "aliases", Required = false, HelpText = "Aliases for the Subject", Default = false)]
    public string? Aliases { get; set; }

    [Option('s', "associated", Required = false, HelpText = "Associated-Subjects for the Subject", Default = false)]
    public string? AssociatedSubjects { get; set; }

    [Option('h', "hashtags", Required = false, HelpText = "Twitter Hashtags the Subject (separated by space)",
        Default = false)]
    public string? HashTags { get; set; }

    [Option('r', "reddit-flair", Required = false, HelpText = "Reddit Flair for the Subject", Default = false)]
    public string? Flair { get; set; }
}