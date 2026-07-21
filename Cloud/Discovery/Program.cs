using Microsoft.Extensions.Hosting;
using Azure;
using Discovery;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
host.Run();