using AgenteDecisao.API.Agents;
using AgenteDecisao.API.Models;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Tools;
using AgenteDecisao.API.Tools.Intarfaces;
using AgendaDecisao.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public async Task ExecutarAsync_QuandoIAFalha_DeveAplicarJustificativaFallback()
    {
        var agente = CriarAgente(exceptionToThrow: new HttpRequestException("OpenAI indisponível"));
        var contexto = CriarContexto(80, "ATIVA");

        var resultado = await agente.ExecutarAsync(contexto);

        Assert.Equal("SUSPENDER", resultado.Decisao);
        Assert.Equal("Decisão aplicada por regra de inadimplência; justificativa automática indisponível no momento.", resultado.Justificativa);
    }

    private static AgenteInadimpleciaDeClientes CriarAgente(string response = "Justificativa padrão", Exception? exceptionToThrow = null)
    {
        var servicoIA = new ServicoIAFake(response, exceptionToThrow);
        var ferramentas = new List<IFerramentaAgente>
        {
            new FerramentaSuspender(),
            new FerramentaCencelar()
        };

        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "unit-test-trace" }
        };

        return new AgenteInadimpleciaDeClientes(
            servicoIA,
            ferramentas,
            NullLogger<AgenteInadimpleciaDeClientes>.Instance,
            contextAccessor);
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

    private sealed class ServicoIAFake(string response, Exception? exceptionToThrow) : IServicoIA
    {
        public Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (exceptionToThrow is not null)
            {
                throw exceptionToThrow;
            }

            return Task.FromResult(response);
        }
    }
}
