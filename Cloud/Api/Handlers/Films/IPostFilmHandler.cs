using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Films;

public interface IPostFilmHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        FilmChangeRequestWrapper request,
        CancellationToken c);
}
