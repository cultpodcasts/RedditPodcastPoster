namespace Api.Dtos;

public enum SearchIndexerState
{
    EpisodeNotFound,
    EpisodeIdConflict,
    NoDocuments,
    Executed,
    Failure,
    TooManyRequests,
    AlreadyRunning,
    Unknown
}