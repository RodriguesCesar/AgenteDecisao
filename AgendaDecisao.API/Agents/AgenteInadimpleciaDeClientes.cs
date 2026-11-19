using AgenteDecisao.API.Models;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Tools.Intarfaces;

namespace AgenteDecisao.API.Agents;

public class AgenteInadimpleciaDeClientes(IServicoIA servicoIA, IEnumerable<IFerramentaAgente> ferramentas)
{
    private static string DeterminarDecisao(int diasInadimplente)
    {
        if (diasInadimplente < 60)
        {
            return "MANTER";
        }

        if (diasInadimplente <= 120)
        {
            return "SUSPENDER";
        }

        return "CANCELAR";
    }

    private static string ConstruirPromptJustificativa(ContextoDecisao contexto, string decisao)
    {
        return $@"
                Você é um assistente de atendimento corporativo.

                A decisão final já foi definida por regra de negócio e não pode ser alterada.
                Decisão definida: {decisao}

                Dados para contexto:
                Cliente: {contexto.Cliente?.Nome}
                Dias em atraso: {contexto.Cliente?.DiasInadimplente}
                Status atual da assinatura: {contexto.Assinatura?.Status}
                Contexto informado pelo usuário: {contexto.EntradaUsuario}

                Gere apenas uma justificativa curta e objetiva para a decisão.
                Sem markdown, sem JSON e sem listas.
                Máximo de 240 caracteres.
            ";
    }

    public async Task<ResultadoDecisao> ExecutarAsync(ContextoDecisao contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Cliente is null)
        {
            throw new InvalidOperationException("Cliente é obrigatório para tomada de decisão.");
        }

        var decisao = DeterminarDecisao(contexto.Cliente.DiasInadimplente);
        var prompt = ConstruirPromptJustificativa(contexto, decisao);
        var justificativa = await servicoIA.ExecutarAsync(prompt, cancellationToken);

        if (string.IsNullOrWhiteSpace(justificativa))
        {
            throw new InvalidOperationException("A IA não retornou justificativa válida para a decisão.");
        }

        var resultado = new ResultadoDecisao
        {
            Decisao = decisao,
            Confianca = 1.0,
            Justificativa = justificativa.Trim()
        };

        await ExecutarFerramenta(decisao, contexto);
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
