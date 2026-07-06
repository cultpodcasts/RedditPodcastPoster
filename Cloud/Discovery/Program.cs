using Azure;
using Discovery;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
await HostCompositionValidator.ValidateAsync(host, "Discovery", Ioc.CompositionCanaryServices);
await host.RunAsync();