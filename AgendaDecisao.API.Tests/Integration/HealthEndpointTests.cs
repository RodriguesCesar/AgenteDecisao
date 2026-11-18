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
}
