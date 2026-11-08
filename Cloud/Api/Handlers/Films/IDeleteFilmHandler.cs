using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Films;

public interface IDeleteFilmHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid filmId,
        CancellationToken c);
}
