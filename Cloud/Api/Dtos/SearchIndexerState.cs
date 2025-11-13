namespace Api.Dtos;

public enum SearchIndexerState
{
    EpisodeNotFound = 1,
    EpisodeIdConflict,
    NoDocuments,
    Executed,
    Failure,
    TooManyRequests,
    AlreadyRunning,
    Unknown
}