# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade Class-Libraries\RedditPodcastPoster.Models\RedditPodcastPoster.Models.csproj
4. Upgrade Class-Libraries\RedditPodcastPoster.Persistence.Abstractions\RedditPodcastPoster.Persistence.Abstractions.csproj
5. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Abstractions\RedditPodcastPoster.PodcastServices.Abstractions.csproj
6. Upgrade Class-Libraries\RedditPodcastPoster.Text\RedditPodcastPoster.Text.csproj
7. Upgrade Third-Party\sirkris-Reddit.NET-1.5.3\src\Reddit.NET\Reddit.NET.csproj
8. Upgrade Class-Libraries\RedditPodcastPoster.Configuration\RedditPodcastPoster.Configuration.csproj
9. Upgrade Class-Libraries\RedditPodcastPoster.Reddit\RedditPodcastPoster.Reddit.csproj
10. Upgrade Class-Libraries\RedditPodcastPoster.Common\RedditPodcastPoster.Common.csproj
11. Upgrade Class-Libraries\RedditPodcastPoster.Cloudflare\RedditPodcastPoster.Cloudflare.csproj
12. Upgrade Class-Libraries\RedditPodcastPoster.Persistence\RedditPodcastPoster.Persistence.csproj
13. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.YouTube\RedditPodcastPoster.PodcastServices.YouTube.csproj
14. Upgrade Class-Libraries\RedditPodcastPoster.BBC\RedditPodcastPoster.BBC.csproj
15. Upgrade Class-Libraries\RedditPodcastPoster.InternetArchive\RedditPodcastPoster.InternetArchive.csproj
16. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Apple\RedditPodcastPoster.PodcastServices.Apple.csproj
17. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Spotify\RedditPodcastPoster.PodcastServices.Spotify.csproj
18. Upgrade Class-Libraries\RedditPodcastPoster.UrlShortening\RedditPodcastPoster.UrlShortening.csproj
19. Upgrade Class-Libraries\RedditPodcastPoster.Subjects\RedditPodcastPoster.Subjects.csproj
20. Upgrade Class-Libraries\RedditPodcastPoster.Search\RedditPodcastPoster.Search.csproj
21. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices\RedditPodcastPoster.PodcastServices.csproj
22. Upgrade Class-Libraries\RedditPodcastPoster.Auth0\RedditPodcastPoster.Auth0.csproj
23. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.ListenNotes\RedditPodcastPoster.PodcastServices.ListenNotes.csproj
24. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Taddy\RedditPodcastPoster.PodcastServices.Taddy.csproj
25. Upgrade Class-Libraries\RedditPodcastPoster.Subreddit\RedditPodcastPoster.Subreddit.csproj
26. Upgrade Class-Libraries\RedditPodcastPoster.PushSubscriptions\RedditPodcastPoster.PushSubscriptions.csproj
27. Upgrade Class-Libraries\RedditPodcastPoster.Twitter\RedditPodcastPoster.Twitter.csproj
28. Upgrade Class-Libraries\RedditPodcastPoster.Bluesky\RedditPodcastPoster.Bluesky.csproj
29. Upgrade Class-Libraries\RedditPodcastPoster.EntitySearchIndexer\RedditPodcastPoster.EntitySearchIndexer.csproj
30. Upgrade Class-Libraries\RedditPodcastPoster.UrlSubmission\RedditPodcastPoster.UrlSubmission.csproj
31. Upgrade Class-Libraries\RedditPodcastPoster.CloudflareRedirect\RedditPodcastPoster.CloudflareRedirect.csproj
32. Upgrade Class-Libraries\RedditPodcastPoster.ContentPublisher\RedditPodcastPoster.ContentPublisher.csproj
33. Upgrade Class-Libraries\RedditPodcastPoster.YouTubePushNotifications\RedditPodcastPoster.YouTubePushNotifications.csproj
34. Upgrade Class-Libraries\RedditPodcastPoster.EdgeApi\RedditPodcastPoster.EdgeApi.csproj
35. Upgrade Cloud\Azure\Azure.csproj
36. Upgrade Class-Libraries\RedditPodcastPoster.Discovery\RedditPodcastPoster.Discovery.csproj
37. Upgrade Class-Libraries\RedditPodcastPoster.Indexing\RedditPodcastPoster.Indexing.csproj
38. Upgrade Console-Apps\TextClassifierTraining\TextClassifierTraining.csproj
39. Upgrade Console-Apps\WikipediaEpisodeEnricher\WikipediaEpisodeEnricher.csproj
40. Upgrade Class-Libraries\RedditPodcastPoster.BBC.Tests\RedditPodcastPoster.BBC.Tests.csproj
41. Upgrade Console-Apps\EliminateExistingEpisodes\EliminateExistingEpisodes.csproj
42. Upgrade Class-Libraries\RedditPodcastPoster.UrlSubmission.Tests\RedditPodcastPoster.UrlSubmission.Tests.csproj
43. Upgrade Console-Apps\EnrichPodcastWithImages\EnrichPodcastWithImages.csproj
44. Upgrade Console-Apps\LaunchSettingsToAppSettings\LaunchSettingsToAppSettings.csproj
45. Upgrade Console-Apps\SecretsToFunctionSettings\SecretsToFunctionSettings.csproj
46. Upgrade Console-Apps\WebsubStatus\WebsubStatus.csproj
47. Upgrade Console-Apps\AddSubjectToSearchMatches\AddSubjectToSearchMatches.csproj
48. Upgrade Console-Apps\ThrowawayConsole\ThrowawayConsole.csproj
49. Upgrade Console-Apps\RenamePodcast\RenamePodcast.csproj
50. Upgrade Console-Apps\FixDatesFromApple\FixDatesFromApple.csproj
51. Upgrade Console-Apps\FlairPublisher\FlairPublisher.csproj
52. Upgrade Console-Apps\MachineAuth0\MachineAuth0.csproj
53. Upgrade Console-Apps\IndexAllEpisodesAudit\IndexAllEpisodesAudit.csproj
54. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Apple.Tests\RedditPodcastPoster.PodcastServices.Apple.Tests.csproj
55. Upgrade Console-Apps\KVWriter\KVWriter.csproj
56. Upgrade Cloud\Discovery\Discovery.csproj
57. Upgrade Cloud\Api\Api.csproj
58. Upgrade Console-Apps\CreateSearchIndex\CreateSearchIndex.csproj
59. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.Spotify.Tests\RedditPodcastPoster.PodcastServices.Spotify.Tests.csproj
60. Upgrade Console-Apps\DeleteSearchDocument\DeleteSearchDocument.csproj
61. Upgrade Console-Apps\CategorisePodcastEpisodes\CategorisePodcastEpisodes.csproj
62. Upgrade Console-Apps\EnrichSubjectRedditFlairs\EnrichSubjectRedditFlairs.csproj
63. Upgrade Console-Apps\JsonSplitCosmosDbUploader\JsonSplitCosmosDbUploader.csproj
64. Upgrade Class-Libraries\RedditPodcastPoster.PodcastServices.YouTube.Tests\RedditPodcastPoster.PodcastServices.YouTube.Tests.csproj
65. Upgrade Class-Libraries\RedditPodcastPoster.YouTubePushNotifications.Tests\RedditPodcastPoster.YouTubePushNotifications.Tests.csproj
66. Upgrade Console-Apps\YouTubePushNotificationSubscribe\YouTubePushNotificationSubscribe.csproj
67. Upgrade Console-Apps\Discover\Discover.csproj
68. Upgrade Console-Apps\Poster\Poster.csproj
69. Upgrade Class-Libraries\RedditPodcastPoster.AI\RedditPodcastPoster.AI.csproj
70. Upgrade Class-Libraries\RedditPodcastPoster.Subjects.Tests\RedditPodcastPoster.Subjects.Tests.csproj
71. Upgrade Console-Apps\SubjectSeeder\SubjectSeeder.csproj
72. Upgrade Class-Libraries\RedditPodcastPoster.Text.Tests\RedditPodcastPoster.Text.Tests.csproj
73. Upgrade Console-Apps\TextClassifierTraining.Tests\TextClassifierTraining.Tests.csproj
74. Upgrade Console-Apps\KVWriter\KVWriter.csproj
75. Upgrade Console-Apps\ModelTransformer\ModelTransformer.csproj
76. Upgrade Console-Apps\EnrichExistingEpisodesFromPodcastServices\EnrichExistingEpisodesFromPodcastServices.csproj
77. Upgrade Console-Apps\AddYouTubeChannelAsPodcast\AddYouTubeChannelAsPodcast.csproj
78. Upgrade Console-Apps\RedditPodcastPoster\RedditPodcastPoster.csproj
79. Upgrade Console-Apps\Sqllite3DatabasePublisher\Sqllite3DatabasePublisher.csproj
80. Upgrade Console-Apps\SubmitUrl\SubmitUrl.csproj
81. Upgrade Console-Apps\SeedEliminationTerms\SeedEliminationTerms.csproj
82. Upgrade Console-Apps\EnrichYouTubeOnlyPodcasts\EnrichYouTubeOnlyPodcasts.csproj
83. Upgrade Console-Apps\AddAudioPodcast\AddAudioPodcast.csproj
84. Upgrade Console-Apps\CultPodcasts.DatabasePublisher\CultPodcasts.DatabasePublisher.csproj
85. Upgrade Console-Apps\EnrichEpisodesFromPostFlare\EnrichEpisodesFromPostFlare.csproj
86. Upgrade Console-Apps\CosmosDbFixer\CosmosDbFixer.csproj
87. Upgrade Console-Apps\CosmosDbDownloader\CosmosDbDownloader.csproj
88. Upgrade Console-Apps\CosmosDbUploader\CosmosDbUploader.csproj
89. Upgrade Console-Apps\ModelTransformer\ModelTransformer.csproj
90. Upgrade Console-Apps\EnrichExistingEpisodesFromPodcastServices\EnrichExistingEpisodesFromPodcastServices.csproj
91. Upgrade Console-Apps\AddYouTubeChannelAsPodcast\AddYouTubeChannelAsPodcast.csproj
92. Upgrade Console-Apps\RedditPodcastPoster\RedditPodcastPoster.csproj
93. Upgrade Console-Apps\Sqllite3DatabasePublisher\Sqllite3DatabasePublisher.csproj
94. Upgrade Console-Apps\SubmitUrl\SubmitUrl.csproj


## Settings

### Excluded projects

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|
|                                                |                            |

### Aggregate NuGet packages modifications across all projects

| Package Name                                      | Current Version    | New Version | Description                                   |
|:--------------------------------------------------|:-----------------:|:-----------:|:----------------------------------------------|
| Microsoft.Azure.Functions.Worker.Sdk              | 2.0.5             | 2.0.7      | Recommended update for Functions SDK          |
| Microsoft.EntityFrameworkCore.Sqlite              | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.EntityFrameworkCore.Tools               | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Configuration                | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Configuration.Abstractions   | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Configuration.UserSecrets    | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.10        | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Hosting                      | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Http                         | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Logging.Abstractions         | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Options                      | 9.0.10            | 10.0.0     | Recommended update for .NET 10                |
| Microsoft.Extensions.Options.ConfigurationExtensions | 9.0.10         | 10.0.0     | Recommended update for .NET 10                |


### Project upgrade details

#### Class-Libraries\RedditPodcastPoster.Models\RedditPodcastPoster.Models.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.* packages should be updated to `10.0.0` where applicable.

Other changes:
  - Review code for any API breaking changes when moving from .NET 9 to .NET 10.

#### Class-Libraries\RedditPodcastPoster.Persistence.Abstractions\RedditPodcastPoster.Persistence.Abstractions.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.* packages should be updated to `10.0.0` where applicable.

Other changes:
  - Review code for any API breaking changes when moving from .NET 9 to .NET 10.

#### Class-Libraries\RedditPodcastPoster.PodcastServices.Abstractions\RedditPodcastPoster.PodcastServices.Abstractions.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.* packages should be updated to `10.0.0` where applicable.

Other changes:
  - Review code for any API breaking changes when moving from .NET 9 to .NET 10.

#### Class-Libraries\RedditPodcastPoster.Text\RedditPodcastPoster.Text.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions -> `10.0.0`

Other changes:
  - Review code for any API breaking changes when moving from .NET 9 to .NET 10.

#### Third-Party\sirkris-Reddit.NET-1.5.3\src\Reddit.NET\Reddit.NET.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - (Security-related Newtonsoft.Json updates are excluded per user request)

Other changes:
  - Verify compatibility of third-party library sources with .NET 10.

#### Class-Libraries\RedditPodcastPoster.Configuration\RedditPodcastPoster.Configuration.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Configuration.Abstractions -> `10.0.0`
  - Microsoft.Extensions.Configuration.UserSecrets -> `10.0.0`
  - Microsoft.Extensions.Logging.Abstractions -> `10.0.0`
  - Microsoft.Extensions.Options -> `10.0.0`
  - Microsoft.Extensions.Options.ConfigurationExtensions -> `10.0.0`

Other changes:
  - Review API usage for breaking changes.

#### Class-Libraries\RedditPodcastPoster.Reddit\RedditPodcastPoster.Reddit.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Logging.Abstractions -> `10.0.0`
  - Microsoft.Extensions.Options -> `10.0.0`
  - Microsoft.Extensions.Options.ConfigurationExtensions -> `10.0.0`

Other changes:
  - Review API usage for breaking changes.

#### Class-Libraries\RedditPodcastPoster.Common\RedditPodcastPoster.Common.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.Extensions.Configuration.Abstractions -> `10.0.0`
  - Microsoft.Extensions.Configuration.UserSecrets -> `10.0.0`
  - Microsoft.Extensions.Logging.Abstractions -> `10.0.0`
  - Microsoft.Extensions.Options -> `10.0.0`
  - Microsoft.Extensions.Options.ConfigurationExtensions -> `10.0.0`

Other changes:
  - Review API usage for breaking changes.

