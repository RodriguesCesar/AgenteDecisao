using AgenteDecisao.API.Services.Interfaces;
using System.Text.Json;

namespace AgenteDecisao.API.Services;

public class ServicoOpenIA(HttpClient httpClient) : IServicoIA
{
    public async Task<string> ExecutarAsync(string prompt)
    {
        var requisicao = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var resposta = await httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", requisicao);
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        //Retornar o texto da resposta
        return json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;
    }
}
