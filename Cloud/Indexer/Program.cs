using Azure;
using Indexer;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
await HostCompositionValidator.ValidateAsync(host, "Indexer", Ioc.CompositionCanaryServices);
await host.RunAsync();