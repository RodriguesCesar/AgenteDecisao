using AgenteDecisao.API.Models;
using AgenteDecisao.API.Services;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Tools.Intarfaces;
namespace AgenteDecisao.API.Agents;

public class AgenteInadimpleciaDeClientes(IServicoIA servicoIA, IEnumerable<IFerramentaAgente> ferramentas)
{

    /// <summary>
    /// Método principal para processar o contexto de decisão 
    /// e tomar ações apropriadas com base nas informações 
    /// de inadimplência do cliente.
    /// </summary>
    private string ConstruirPrompt(ContextoDecisao contexto)
    {
        return $@"
                Você é um agente decisor corporativo.

                Regras obrigatórias:
                - Nunca cancelar se atraso < 60 dias
                - Suspender entre 60 e 120 dias
                - Cancelar acima de 120 dias

                Cliente: {contexto?.Cliente?.Nome}
                Dias em atraso: {contexto?.Cliente?.DiasInadimplente}
                Status atual: {contexto?.Assinatura?.Status}
                Contexto do cliente : {contexto?.EntradaUsuario}
                Responda apenas JSON válido:
                {{
                  ""decisao"": ""SUSPENDER | CANCELAR | MANTER"",
                  ""confianca"": 0-1,
                  ""justificativa"": ""motivo""
                }}

                Não utilize markdown.
                Não utilize ```json.
                Não explique nada.
                Apenas retorne o objeto JSON.
            ";
    }
    public async Task<ResultadoDecisao?> ExecutarAsync(ContextoDecisao contexto)
    {
        var prompt = ConstruirPrompt(contexto);
        var resposta = await servicoIA.ExecutarAsync(prompt);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var resultado = System.Text.Json.JsonSerializer.Deserialize<ResultadoDecisao>(resposta, options);
        if (resultado != null)
        {
            await ExecutarFerramenta(resultado.Decisao!, contexto);
        }
        return resultado;

    }
    private async Task ExecutarFerramenta(string decisao, ContextoDecisao contexto)
    {
        var ferramenta = ferramentas.FirstOrDefault(f => f.Nome.Equals(decisao, StringComparison.OrdinalIgnoreCase));
        if (ferramenta != null)
        {
            await ferramenta.ExecutarAsync(contexto);
        }
    }
}
