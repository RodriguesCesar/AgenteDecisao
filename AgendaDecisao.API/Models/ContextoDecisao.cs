using AgendaDecisao.API.Models;
using System.ComponentModel.DataAnnotations;

namespace AgenteDecisao.API.Models;

/// <summary>
/// Este objeto representa o contexto de decisão, contendo informações relevantes
/// para a tomada de decisão. Ele pode incluir dados como o estado atual do sistema,
/// variáveis de ambiente, histórico de decisões anteriores, e quaisquer outros
/// fatores que possam influenciar a decisão a ser tomada.
/// </summary>
public class ContextoDecisao
{
    [Required]
    [MinLength(3)]
    public string EntradaUsuario { get; set; } = string.Empty;

    [Required]
    public Cliente? Cliente { get; set; }

    [Required]
    public Assinatura? Assinatura { get; set; }
}
