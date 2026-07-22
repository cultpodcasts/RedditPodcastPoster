using Api.Dtos;
using Api.Models;

namespace Api.Services.Subjects;

public interface ISubjectCreateService
{
    Task<SubjectCreateResult> CreateAsync(Subject subject, CancellationToken cancellationToken);
}
