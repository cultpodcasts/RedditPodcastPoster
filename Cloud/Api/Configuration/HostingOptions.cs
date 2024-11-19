namespace Api.Configuration;

public class HostingOptions
{
    public string[] UserRoles { get; set; } = [];
    public bool TestMode { get; set; }
}