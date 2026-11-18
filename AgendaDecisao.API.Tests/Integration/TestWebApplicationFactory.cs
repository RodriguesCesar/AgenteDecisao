using AgenteDecisao.API.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenteDecisao.API.Tests.Integration;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeServicoIA FakeServico { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "test-key"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IServicoIA>(FakeServico);
        });
    }
}
