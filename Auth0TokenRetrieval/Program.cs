using System.Reflection;
using System.Text.Json;
using Auth0TokenRetrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RestSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());


builder.Services
    .AddOptions<Auth0ClientSettings>().Bind(builder.Configuration.GetSection("auth0client"));

using var host = builder.Build();

var auth0ClientSettings= host.Services.GetService<IOptions<Auth0ClientSettings>>()?.Value;

var client = new RestClient("https://dev-am0zezgvqozmkpbp.uk.auth0.com/oauth/token");
var request = new RestRequest(Method.POST);
request.AddHeader("content-type", "application/json");
request.AddParameter("application/json", $"{{\"client_id\":\"{auth0ClientSettings.ClientId}\",\"client_secret\":\"{auth0ClientSettings.ClientSecret}\",\"audience\":\"https://cultpodcasts.com/api\",\"grant_type\":\"client_credentials\"}}", ParameterType.RequestBody);
IRestResponse response = client.Execute(request);
Console.Write(JsonSerializer.Serialize(response));