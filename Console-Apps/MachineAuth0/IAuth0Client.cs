namespace MachineAuth0;

public interface IAuth0Client
{
    Task<string> GetClientToken();
}