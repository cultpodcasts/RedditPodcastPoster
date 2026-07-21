using Microsoft.Extensions.Hosting;
using Azure;
using Indexer;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
host.Run();