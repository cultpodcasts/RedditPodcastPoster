using API.Data;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HomePageController : ControllerBase
{
    private readonly ILogger<HomePageController> _logger;
    private readonly IQueryExecutor _queryExecutor;

    public HomePageController(IQueryExecutor queryExecutor, ILogger<HomePageController> logger)
    {
        _queryExecutor = queryExecutor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken ct)
    {
        _logger.LogInformation($"{nameof(Get)} initiated.");
        var homepageModel = await _queryExecutor.GetHomePage(ct);
        _logger.LogInformation($"{nameof(Get)} complete.");
        return Ok(homepageModel);
    }
}