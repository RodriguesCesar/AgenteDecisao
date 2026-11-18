using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace AgenteDecisao.API.Tests.Integration;

public class AgenteRateLimitingTests
{
    [Fact]
    public async Task PostAgente_ExcedendoLimite_DeveRetornarTooManyRequests()
    {
        using var factory = new TestWebApplicationFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:Agente:PermitLimit"] = "1",
            ["RateLimiting:Agente:WindowSeconds"] = "60",
            ["RateLimiting:Agente:QueueLimit"] = "0"
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add("X-Agente-Api-Key", TestWebApplicationFactory.TestApiKey);

        var payload = new
        {
            entradaUsuario = "Cliente com atraso relevante",
            cliente = new { nome = "Cliente Teste", diasInadimplente = 80 },
            assinatura = new { status = "ATIVA" }
        };

        var firstResponse = await client.PostAsJsonAsync("/api/agente", payload);
        var secondResponse = await client.PostAsJsonAsync("/api/agente", payload);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal((HttpStatusCode)429, secondResponse.StatusCode);
    }
}
