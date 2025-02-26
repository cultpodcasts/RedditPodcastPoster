using Azure;
using Discovery;
using Microsoft.Extensions.Hosting;

var host = HostFactory.Create<Program>(Ioc.ConfigureServices);
host.Run();