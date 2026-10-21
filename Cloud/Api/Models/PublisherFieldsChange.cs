namespace Api.Models;

/// <summary>Curator publisher-field edit (news organisation, TV show, film). Null = unchanged.</summary>
public abstract class PublisherFieldsChange
{
    public string? PublisherName { get; set; }
    public string? Description { get; set; }
    public bool? Removed { get; set; }
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

    /// <summary>Children denormalise the publisher's search terms, language and removed (as <c>parentRemoved</c>).</summary>
    public bool ChangesChildProjection => SearchTerms != null || Language != null || Removed != null;
}
