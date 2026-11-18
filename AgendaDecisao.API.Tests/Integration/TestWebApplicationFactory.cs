using AgenteDecisao.API.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenteDecisao.API.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key";

    public FakeServicoIA FakeServico { get; } = new();

    public IDictionary<string, string?> Overrides { get; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var config = new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key",
                ["Authentication:AgenteApiKey"] = TestApiKey,
                ["Authentication:AllowAnonymousInDevelopment"] = "false",
                ["RateLimiting:Agente:PermitLimit"] = "20",
                ["RateLimiting:Agente:WindowSeconds"] = "60",
                ["RateLimiting:Agente:QueueLimit"] = "0"
            };

            foreach (var item in Overrides)
            {
                config[item.Key] = item.Value;
            }

            configBuilder.AddInMemoryCollection(config);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IServicoIA>(FakeServico);
        });
    }
}
