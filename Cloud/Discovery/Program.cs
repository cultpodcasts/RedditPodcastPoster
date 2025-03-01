using Azure;
using Discovery;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create(args, Ioc.ConfigureServices);
host.Run();