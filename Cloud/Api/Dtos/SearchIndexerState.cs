using Api.Contracts;

namespace Api.Dtos;

[ExportContract]
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