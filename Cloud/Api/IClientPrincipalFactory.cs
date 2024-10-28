using Api.Auth;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api;

public interface IClientPrincipalFactory
{
    public ClientPrincipal? Create(HttpRequestData request);
}