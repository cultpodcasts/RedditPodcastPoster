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

    [DbFunction]
    public string Highlight(string match, string column, string open, string close)
    {
        throw new NotImplementedException();
    }

    [DbFunction]
    public string Snippet(string match, string column, string open, string close, string ellips, int count)
    {
        throw new NotImplementedException();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PodcastText>(x =>
        {
            x.HasKey(fts => fts.RowId);

            x.Property(fts => fts.Match).HasColumnName(nameof(PodcastText));

            x.HasOne(fts => fts.Podcast)
                .WithOne(p => p.Text)
                .HasForeignKey<PodcastText>(fts => fts.RowId);
        });
        modelBuilder.Entity<EpisodeText>(x =>
        {
            x.HasKey(fts => fts.RowId);

            x.Property(fts => fts.Match).HasColumnName(nameof(EpisodeText));

            x.HasOne(fts => fts.Episode)
                .WithOne(p => p.Text)
                .HasForeignKey<EpisodeText>(fts => fts.RowId);
        });
        base.OnModelCreating(modelBuilder);
    }
}