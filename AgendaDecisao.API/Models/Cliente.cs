namespace AgenteDecisao.API.Models;

/// <summary>
/// Representa os dados que alimentam a decisão
/// </summary>
public class Cliente
{
    public string Nome { get; set; } = string.Empty;
    public int DiasInadimplente { get; set; }
}
