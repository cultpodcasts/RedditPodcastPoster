using Microsoft.EntityFrameworkCore;

namespace Sqllite3DatabasePublisher;

public class DatabaseContext : DbContext
{
    private readonly DbContextOptions<DatabaseContext> _options;

    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
        _options = options;
    }

    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<Subject> Subjects { get; set; }

 
}