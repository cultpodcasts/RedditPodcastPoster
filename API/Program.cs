using API.Data;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("API_");

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddApplicationInsightsTelemetry(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])
    .AddScoped<IQueryExecutor, QueryExecutor>()
    .AddScoped<ITextSanitiser, TextSanitiser>()
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));

CosmosDbClientFactory.AddCosmosClient(builder.Services);

builder.Services.AddControllers();

var app = builder.Build();

//Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();