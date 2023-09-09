using RedditPodcastPoster.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sqllite3DatabasePublisher;

public class Podcast
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public virtual List<Episode> Episodes { get; set; }
    public Service? PrimaryPostService { get; set; }
    public PodcastText Text { get; set; }
}

public class PodcastText
{
    public int RowId { get; set; }
    public Podcast Podcast { get; set; }
    public string Name { get; set; }
    public string Publisher { get; set; }
    public string Match { get; set; }
    public double? Rank { get; set; }
}