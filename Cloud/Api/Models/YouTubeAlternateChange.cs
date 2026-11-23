namespace Api.Models;

/// <summary>One additional YouTube entry to keep or add in a film (or other playable) PATCH.</summary>
public sealed record YouTubeAlternateChange(string? YouTubeId, string? Url, string? Title, string? ChannelName);
