using Azure;
using Indexer;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create<Program>(args, Ioc.ConfigureServices);
host.Run();