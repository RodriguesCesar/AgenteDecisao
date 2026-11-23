using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace AgenteDecisao.API.Tests.Integration;

public class AgenteRateLimitPorClienteTests : IClassFixture<LimitedRateWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public AgenteRateLimitPorClienteTests(LimitedRateWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        _httpClient.DefaultRequestHeaders.Add("X-Agente-Api-Key", LimitedRateWebApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task PostAgente_RateLimitDeveSerSegmentadoPorCliente()
    {
        var primeiraClienteA = await PostAgenteAsync("cliente-a");
        var segundaClienteA = await PostAgenteAsync("cliente-a");
        var primeiraClienteB = await PostAgenteAsync("cliente-b");

        Assert.Equal(HttpStatusCode.OK, primeiraClienteA.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, segundaClienteA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, primeiraClienteB.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAgenteAsync(string clientId)
    {
        var payload = new
        {
            entradaUsuario = "Cliente com atraso relevante",
            cliente = new { nome = "Cliente Teste", diasInadimplente = 80 },
            assinatura = new { status = "ATIVA" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agente")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Agente-Client-Id", clientId);

        return await _httpClient.SendAsync(request);
    }
}
