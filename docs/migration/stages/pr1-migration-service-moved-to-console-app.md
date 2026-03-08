# PR1 Stage Note: Legacy-to-V2 migration logic moved to console app

## Change
- Removed migration service and contract from persistence assemblies:
  - `Class-Libraries/RedditPodcastPoster.Persistence/LegacyPodcastToV2MigrationService.cs`
  - `Class-Libraries/RedditPodcastPoster.Persistence.Abstractions/ILegacyPodcastToV2MigrationService.cs`
- Removed migration DI registration from persistence `AddRepositories()`.

## Added console app
- `Console-Apps/LegacyPodcastToV2Migration/LegacyPodcastToV2Migration.csproj`
- `Console-Apps/LegacyPodcastToV2Migration/Program.cs`
- `Console-Apps/LegacyPodcastToV2Migration/LegacyPodcastToV2MigrationProcessor.cs`

## Why
- Migration code is temporary and should not remain in the long-lived persistence assembly.
- Keeps runtime libraries clean while preserving migration capability during cutover.
