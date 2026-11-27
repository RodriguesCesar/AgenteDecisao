using AgenteDecisao.API.Services;
using AgenteDecisao.API.Settings;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AgenteDecisao.API.Tests.Services;

public class ServicoOpenIATests
{
    [Fact]
    public async Task ExecutarAsync_DeveUsarModelConfiguradoNaRequisicao()
    {
        var handler = new CaptureHandler("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAISettings
        {
            ApiKey = "test-key",
            Model = "gpt-4.1-mini",
            TimeoutSeconds = 30
        });

        var servico = new ServicoOpenIA(httpClient, options);

        var resultado = await servico.ExecutarAsync("prompt de teste");

        Assert.Equal("ok", resultado);
        Assert.NotNull(handler.RequestContent);

        using var doc = JsonDocument.Parse(handler.RequestContent!);
        var model = doc.RootElement.GetProperty("model").GetString();
        Assert.Equal("gpt-4.1-mini", model);
    }

    private sealed class CaptureHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                RequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
