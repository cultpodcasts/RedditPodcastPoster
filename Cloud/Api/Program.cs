using Api;
using Azure;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
await HostCompositionValidator.ValidateAsync(host, "Api", Ioc.CompositionCanaryServices);
await host.RunAsync();