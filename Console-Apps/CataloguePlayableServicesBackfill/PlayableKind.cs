namespace CataloguePlayableServicesBackfill;

public enum PlayableKind
{
    Film,
    TvShowEpisode,
    NewsReport
}

public static class PlayableKindParser
{
    public static bool TryParseSelection(string kind, out IReadOnlyList<PlayableKind> kinds)
    {
        kinds = [];
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        switch (kind.Trim().ToLowerInvariant())
        {
            case "all":
                kinds = [PlayableKind.Film, PlayableKind.TvShowEpisode, PlayableKind.NewsReport];
                return true;
            case "film":
                kinds = [PlayableKind.Film];
                return true;
            case "tvshowepisode":
            case "tv-show-episode":
            case "tv":
                kinds = [PlayableKind.TvShowEpisode];
                return true;
            case "newsreport":
            case "news-report":
            case "news":
                kinds = [PlayableKind.NewsReport];
                return true;
            default:
                return false;
        }
    }

    public static string ToWire(PlayableKind kind) => kind switch
    {
        PlayableKind.Film => "film",
        PlayableKind.TvShowEpisode => "tvshowepisode",
        PlayableKind.NewsReport => "newsreport",
        _ => kind.ToString().ToLowerInvariant()
    };
}
