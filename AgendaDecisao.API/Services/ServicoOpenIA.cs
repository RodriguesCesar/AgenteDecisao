using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Settings;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AgenteDecisao.API.Services;

public class ServicoOpenIA(HttpClient httpClient, IOptions<OpenAISettings> openAISettings) : IServicoIA
{
    public async Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("O prompt não pode ser vazio.", nameof(prompt));
        }

        var requisicao = new
        {
            model = openAISettings.Value.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var resposta = await httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions",
            requisicao,
            cancellationToken);

        resposta.EnsureSuccessStatusCode();

        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        if (!json.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Resposta da OpenAI não contém escolhas válidas.");
        }

        var content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Resposta da OpenAI não contém conteúdo textual válido.");
        }

        return content;
    }
}
