using Microsoft.EntityFrameworkCore;

namespace Sqllite3DatabasePublisher;

public class PodcastContext : DbContext
{
    private readonly DbContextOptions<PodcastContext> _options;

    public PodcastContext(DbContextOptions<PodcastContext> options) : base(options)
    {
        _options = options;
    }

    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<Episode> Episodes { get; set; }

 
}