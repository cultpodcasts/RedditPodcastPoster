using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Subjects;

public interface IPostSubjectHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper,
        CancellationToken c);
}
