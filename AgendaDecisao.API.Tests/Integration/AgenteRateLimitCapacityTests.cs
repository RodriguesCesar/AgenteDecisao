using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace AgenteDecisao.API.Tests.Integration;

public class AgenteRateLimitCapacityTests : IClassFixture<CapacityLimitedRateWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public AgenteRateLimitCapacityTests(CapacityLimitedRateWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        _httpClient.DefaultRequestHeaders.Add("X-Agente-Api-Key", CapacityLimitedRateWebApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task PostAgente_ComBucketsNoLimite_DeveRetornarTooManyRequestsParaNovoCliente()
    {
        var primeiraResposta = await PostAgenteAsync("cliente-a");
        var segundaResposta = await PostAgenteAsync("cliente-b");

        Assert.Equal(HttpStatusCode.OK, primeiraResposta.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, segundaResposta.StatusCode);
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
