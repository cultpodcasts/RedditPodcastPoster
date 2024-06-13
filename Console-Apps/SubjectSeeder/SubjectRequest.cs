using CommandLine;

namespace SubjectSeeder;

public class SubjectRequest
{
    [Value(0, MetaName = "subject-name", HelpText = "The name of the subject", Required = true)]
    public required string Name { get; set; }

    [Option('a', "aliases", Required = false, HelpText = "Aliases for the Subject", Default = null)]
    public string? Aliases { get; set; }

    [Option('s', "associated", Required = false, HelpText = "Associated-Subjects for the Subject", Default = null)]
    public string? AssociatedSubjects { get; set; }

    [Option('h', "hashtags", Required = false, HelpText = "Twitter Hashtags the Subject (separated by space)",
        Default = null)]
    public string? HashTags { get; set; }

    [Option('r', "reddit-flair", Required = false, HelpText = "Reddit Flair for the Subject", Default = null)]
    public string? Flair { get; set; }

    [Option('c', "create-reddit-flair", Default = false, Required = false,
        HelpText = "Reddit Flair for the Subject, rather than a recycled one")]
    public bool CreateFlair { get; set; }

    [Option('n', "recycled-flair-name", Required = false, HelpText = "Name of the recycled-flair", Default = null)]
    public string? RecycledFlairName { get; set; }
}