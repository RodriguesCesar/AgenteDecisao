using System.ComponentModel.DataAnnotations;

namespace AgenteDecisao.API.Models;

/// <summary>
/// Representa os dados que alimentam a decisão
/// </summary>
public class Cliente
{
    [Required]
    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Range(0, 3650)]
    public int DiasInadimplente { get; set; }
}
