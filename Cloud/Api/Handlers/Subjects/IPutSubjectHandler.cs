using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Subjects;

public interface IPutSubjectHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubjectChangeRequest subject,
        CancellationToken ct);
}
