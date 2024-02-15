namespace Discover;

public record EpisodeResult(
    string Id,
    DateTime Released,
    string Description,
    string EpisodeName,
    string ShowName,
    Uri? Url = null);