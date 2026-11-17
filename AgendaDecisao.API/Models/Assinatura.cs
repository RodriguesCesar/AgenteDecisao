using System.ComponentModel.DataAnnotations;

namespace AgendaDecisao.API.Models;

/// <summary>
/// Representa o estado do recurso que pode ser alterado pelo agente de IA
/// </summary>
public class Assinatura
{
    [Required]
    [RegularExpression("ATIVA|SUSPENSA|CANCELADA")]
    public string Status { get; set; } = "ATIVA";
}
