using Microsoft.AspNetCore.Mvc.Testing;

namespace AgenteDecisao.API.Tests.Integration;

public class HealthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task GetHealth_DeveRetornarOk()
    {
        var response = await _httpClient.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetHealthLive_DeveRetornarOk()
    {
        var response = await _httpClient.GetAsync("/health/live");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetHealthReady_DeveRetornarOk()
    {
        var response = await _httpClient.GetAsync("/health/ready");

        Assert.True(response.IsSuccessStatusCode);
    }
}
