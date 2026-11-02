using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Api;
using Api.Middleware;
using Azure;

var host = HostFactory.Create(
    args,
    Ioc.ConfigureServices,
    worker => worker.UseMiddleware<FunctionInputBindingBadRequestMiddleware>());
host.Run();
