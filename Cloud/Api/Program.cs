using Api;
using Azure;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create<Program>(Ioc.ConfigureServices);
host.Run();