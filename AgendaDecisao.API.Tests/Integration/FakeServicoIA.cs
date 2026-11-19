using AgenteDecisao.API.Services.Interfaces;

namespace AgenteDecisao.API.Tests.Integration;

public class FakeServicoIA : IServicoIA
{
    public string Response { get; set; } = "{\"decisao\":\"MANTER\",\"confianca\":0.9,\"justificativa\":\"ok\"}";

    public Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(Response);
}
