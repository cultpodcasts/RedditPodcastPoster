using Microsoft.Extensions.Hosting;
using Api;
using Azure;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
host.Run();