using AgenteDecisao.API.Services.Interfaces;

namespace AgenteDecisao.API.Tests.Integration;

public class FakeServicoIA : IServicoIA
{
    public string Response { get; set; } = "{\"decisao\":\"MANTER\",\"confianca\":0.9,\"justificativa\":\"ok\"}";

    public Exception? ExceptionToThrow { get; set; }

    public Task<string> ExecutarAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(Response);
    }
}
