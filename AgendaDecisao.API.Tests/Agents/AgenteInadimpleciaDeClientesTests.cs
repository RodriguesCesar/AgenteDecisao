using AgenteDecisao.API.Agents;
using AgenteDecisao.API.Models;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Tools;
using AgenteDecisao.API.Tools.Intarfaces;
using AgendaDecisao.API.Models;

namespace AgenteDecisao.API.Tests.Agents;

public class AgenteInadimpleciaDeClientesTests
{
    [Fact]
    public async Task ExecutarAsync_ComDecisaoCancelar_DeveAplicarFerramenta()
    {
        var servicoIA = new ServicoIAFake("{\"decisao\":\"CANCELAR\",\"confianca\":0.99,\"justificativa\":\"atraso acima de 120 dias\"}");
        var ferramentas = new List<IFerramentaAgente>
        {
            new FerramentaSuspender(),
            new FerramentaCencelar()
        };

        var agente = new AgenteInadimpleciaDeClientes(servicoIA, ferramentas);

        var contexto = new ContextoDecisao
        {
            EntradaUsuario = "Cliente sem perspectiva de regularização",
            Cliente = new Cliente { Nome = "Cliente Teste", DiasInadimplente = 150 },
            Assinatura = new Assinatura { Status = "ATIVA" }
        };

        var resultado = await agente.ExecutarAsync(contexto);

        Assert.Equal("CANCELAR", resultado.Decisao);
        Assert.Equal("CANCELADA", contexto.Assinatura?.Status);
    }

    private sealed class ServicoIAFake(string response) : IServicoIA
    {
        public Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(response);
    }
}
