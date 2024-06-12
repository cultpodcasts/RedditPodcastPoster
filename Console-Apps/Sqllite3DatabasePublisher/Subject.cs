using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Sqllite3DatabasePublisher;

public class Subject
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    public Guid Guid { get; set; }
    public required string Name { get; set; }

}

