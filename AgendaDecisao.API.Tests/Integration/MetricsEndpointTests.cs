using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AgenteDecisao.API.Tests.Integration;

public class MetricsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _httpClient;
    private readonly TestWebApplicationFactory _factory;

    public MetricsEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task GetMetrics_AposFalhaDeAuth_DeveExporContadoresDeSeguranca()
    {
        var payload = new
        {
            entradaUsuario = "Cliente com atraso relevante",
            cliente = new { nome = "Cliente Teste", diasInadimplente = 80 },
            assinatura = new { status = "ATIVA" }
        };

        var unauthorizedResponse = await _httpClient.PostAsJsonAsync("/api/agente", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        var metricsResponse = await _httpClient.GetAsync("/metrics");
        metricsResponse.EnsureSuccessStatusCode();

        var metrics = await metricsResponse.Content.ReadAsStringAsync();

        Assert.Contains("agente_auth_success_total", metrics);
        Assert.Contains("agente_auth_failures_total", metrics);
        Assert.Contains("agente_auth_config_errors_total", metrics);
        Assert.Contains("agente_rate_limit_rejections_total", metrics);
        Assert.Contains("agente_rate_limit_capacity_rejections_total", metrics);
        Assert.Contains("agente_rate_limit_invalid_client_id_total", metrics);
        Assert.Contains("agente_justificativa_fallback_total", metrics);

        var match = Regex.Match(metrics, @"agente_auth_failures_total\s+(\d+)");
        Assert.True(match.Success);
        Assert.True(long.TryParse(match.Groups[1].Value, out var value));
        Assert.True(value >= 1);
    }
}
