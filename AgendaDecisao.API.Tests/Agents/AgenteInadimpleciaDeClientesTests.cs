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
    public async Task ExecutarAsync_ComAtrasoMenorQue60_DeveManterAssinaturaAtiva()
    {
        var agente = CriarAgente("Cliente em dia com plano de regularização.");
        var contexto = CriarContexto(30, "ATIVA");

        var resultado = await agente.ExecutarAsync(contexto);

        Assert.Equal("MANTER", resultado.Decisao);
        Assert.Equal(1.0, resultado.Confianca);
        Assert.Equal("ATIVA", contexto.Assinatura?.Status);
    }

    [Fact]
    public async Task ExecutarAsync_ComAtrasoEntre60e120_DeveSuspenderAssinatura()
    {
        var agente = CriarAgente("Atraso relevante, mas com possibilidade de regularização.");
        var contexto = CriarContexto(60, "ATIVA");

        var resultado = await agente.ExecutarAsync(contexto);

        Assert.Equal("SUSPENDER", resultado.Decisao);
        Assert.Equal(1.0, resultado.Confianca);
        Assert.Equal("SUSPENSA", contexto.Assinatura?.Status);
    }

    [Fact]
    public async Task ExecutarAsync_ComAtrasoAcimaDe120_DeveCancelarAssinatura()
    {
        var agente = CriarAgente("Atraso crítico sem previsão de normalização.");
        var contexto = CriarContexto(121, "ATIVA");

        var resultado = await agente.ExecutarAsync(contexto);

        Assert.Equal("CANCELAR", resultado.Decisao);
        Assert.Equal(1.0, resultado.Confianca);
        Assert.Equal("CANCELADA", contexto.Assinatura?.Status);
    }

    private static AgenteInadimpleciaDeClientes CriarAgente(string justificativaIA)
    {
        var servicoIA = new ServicoIAFake(justificativaIA);
        var ferramentas = new List<IFerramentaAgente>
        {
            new FerramentaSuspender(),
            new FerramentaCencelar()
        };

        return new AgenteInadimpleciaDeClientes(servicoIA, ferramentas);
    }

    private static ContextoDecisao CriarContexto(int diasInadimplente, string statusAssinatura)
    {
        return new ContextoDecisao
        {
            EntradaUsuario = "Cliente informou situação financeira temporária.",
            Cliente = new Cliente { Nome = "Cliente Teste", DiasInadimplente = diasInadimplente },
            Assinatura = new Assinatura { Status = statusAssinatura }
        };
    }

    private sealed class ServicoIAFake(string response) : IServicoIA
    {
        public Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(response);
    }
}
