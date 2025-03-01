using Azure;
using Indexer;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
host.Run();