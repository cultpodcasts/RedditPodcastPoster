using Api.Dtos;

namespace Api.Models;

public record SubjectChangeRequestWrapper(Guid? SubjectId, Subject Subject);
