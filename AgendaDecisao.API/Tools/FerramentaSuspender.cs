using AgenteDecisao.API.Models;
using AgenteDecisao.API.Tools.Intarfaces;

namespace AgenteDecisao.API.Tools;

public class FerramentaSuspender: IFerramentaAgente
{
    public string Nome => "SUSPENDER";

    public Task ExecutarAsync(ContextoDecisao contexto)
    {
        contexto.Assinatura!.Status = "SUSPENSA";
        return Task.CompletedTask;
    }
}
