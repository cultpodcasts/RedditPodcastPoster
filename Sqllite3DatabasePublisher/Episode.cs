using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sqllite3DatabasePublisher;

public class Episode
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    public Guid Guid { get; set; }
    public DateTime Release { get; set; }
    public TimeSpan Length { get; set; }
    public bool Explicit { get; set; }
    public EpisodeText Text { get; set; }
}

public class EpisodeText
{
    public int RowId { get; set; }
    public Episode Episode { get; set; }
    public string Subjects { get; set; }
    public Uri? YouTube { get; set; }
    public Uri? Spotify { get; set; }
    public Uri? Apple { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Match { get; set; }
    public double? Rank { get; set; }
}