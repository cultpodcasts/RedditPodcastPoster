namespace Indexer;

public record IndexerResponse(
    bool Success, 
    bool SkipYouTubeUrlResolving, 
    bool YouTubeError,
    bool SkipSpotifyUrlResolving, 
    bool SpotifyError);