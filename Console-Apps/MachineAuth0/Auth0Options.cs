namespace MachineAuth0;

public class Auth0Options
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string Audience { get; set; }
    public required string Domain { get; set; }
}