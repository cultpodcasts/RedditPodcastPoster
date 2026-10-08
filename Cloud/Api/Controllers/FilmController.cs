using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.Films;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class FilmController(
    IGetFilmHandler getFilmHandler,
    IPostFilmHandler postFilmHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<FilmController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("FilmGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "film/{filmIdentifier}")]
        HttpRequestData req,
        string filmIdentifier,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], filmIdentifier, getFilmHandler.Handle, Unauthorised, ct);

    [Function("FilmPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "film/{filmId:guid}")]
        HttpRequestData req,
        Guid filmId,
        [FromBody] Dtos.FilmChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new FilmChangeRequestWrapper(filmId, change.ToModel()),
            postFilmHandler.Handle,
            Unauthorised,
            ct);
}
