namespace Api.Models;

/// <summary>Curator playable-field edit (news report, TV-show episode). Null = unchanged.</summary>
public abstract class PlayableFieldsChange
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Release { get; set; }
    public string? Duration { get; set; }
    public bool? Posted { get; set; }
    public bool? Tweeted { get; set; }
    public bool? UnBluesky { get; set; }
    public bool? Explicit { get; set; }
    public bool? Ignored { get; set; }
    public bool? Removed { get; set; }
    public string[]? Subjects { get; set; }
    public string? SearchTerms { get; set; }
    public string? HashTag { get; set; }
    public string? Language { get; set; }
    public string[]? Guests { get; set; }
}
