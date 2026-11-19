using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace AgenteDecisao.API.Tests.Integration;

public class AgenteEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _httpClient;
    private readonly TestWebApplicationFactory _factory;

    public AgenteEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        _httpClient.DefaultRequestHeaders.Add("X-Agente-Api-Key", TestWebApplicationFactory.TestApiKey);
    }

    [Fact]
    public async Task PostAgente_ComPayloadValido_DeveRetornarOk()
    {
        _factory.FakeServico.Response = "{\"decisao\":\"SUSPENDER\",\"confianca\":0.8,\"justificativa\":\"atraso na faixa de suspensão\"}";

        var payload = new
        {
            entradaUsuario = "Cliente com atraso relevante",
            cliente = new { nome = "Cliente Teste", diasInadimplente = 80 },
            assinatura = new { status = "ATIVA" }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/agente", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAgente_ComPayloadInvalido_DeveRetornarBadRequest()
    {
        var payload = new
        {
            entradaUsuario = "",
            cliente = new { nome = "", diasInadimplente = -10 },
            assinatura = new { status = "INVALIDO" }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/agente", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAgente_SemApiKey_DeveRetornarUnauthorized()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var payload = new
        {
            entradaUsuario = "Cliente com atraso relevante",
            cliente = new { nome = "Cliente Teste", diasInadimplente = 80 },
            assinatura = new { status = "ATIVA" }
        };

        var response = await client.PostAsJsonAsync("/api/agente", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
