using AgenteDecisao.API.Models;
using AgenteDecisao.API.Tools.Intarfaces;

namespace AgenteDecisao.API.Tools;

public class FerramentaCencelar :  IFerramentaAgente
{
    public string Nome => "CANCELAR";

    public Task ExecutarAsync(ContextoDecisao contexto)
    {
        contexto.Assinatura!.Status = "CANCELADA";
        return Task.CompletedTask;
    }
}
