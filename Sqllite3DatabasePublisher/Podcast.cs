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
    public string Name { get; set; }
    public string Publisher { get; set; }
}

