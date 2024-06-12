using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sqllite3DatabasePublisher;

public class Podcast
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Name { get; set; } = "";
    public string? Publisher { get; set; } = "";
}