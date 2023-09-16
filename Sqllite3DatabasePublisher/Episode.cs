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
    public string Subjects { get; set; }
    public Uri? YouTube { get; set; }
    public Uri? Spotify { get; set; }
    public Uri? Apple { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

