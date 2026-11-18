using AgenteDecisao.API.Agents;
using AgenteDecisao.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgenteDecisao.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AgenteController(AgenteInadimpleciaDeClientes agente) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> TomarDecisao([FromBody] ContextoDecisao decisao, CancellationToken cancellationToken)
    {
        var resultado = await agente.ExecutarAsync(decisao, cancellationToken);
        return Ok(resultado);
    }
}
