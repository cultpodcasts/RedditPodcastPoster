﻿using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using SubjectSeeder;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    //.AddSingleton<IFileRepositoryFactory, FileRepositoryFactory>()
    //.AddScoped(s => (IDataRepository) s.GetService<IFileRepositoryFactory>().Create())
    //.AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddRepositories(builder.Configuration)
    .AddSubjectServices()
    .AddSingleton<SubjectsSeeder>();


using var host = builder.Build();
var processor = host.Services.GetService<SubjectsSeeder>();
await processor!.Run();