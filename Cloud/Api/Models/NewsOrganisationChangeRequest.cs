namespace Api.Models;

public class NewsOrganisationChangeRequest
{
    public string? Language { get; set; }
    public string? TwitterHandle { get; set; }
    public string? BlueskyHandle { get; set; }
    public string? HashTag { get; set; }
    public string[]? EnrichmentHashTags { get; set; }
    public string? SearchTerms { get; set; }
    public string[]? KnownTerms { get; set; }
    public string? DefaultSubject { get; set; }
    public string[]? IgnoredSubjects { get; set; }
    public string[]? IgnoredAssociatedSubjects { get; set; }
}
