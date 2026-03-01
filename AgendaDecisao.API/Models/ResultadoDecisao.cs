namespace AgenteDecisao.API.Models;

/// <summary>
/// Representa o resultado de uma decisão em um processo ou operação realizado pelo 
/// Agente de IA. Este objeto pode conter informações como o resultado da decisão, 
/// ações recomendadas, justificativas para a decisão tomada etc.
/// </summary>
public class ResultadoDecisao
{
    public string? Decisao { get; set; }
    public double Confianca { get; set; }
    public string? Justificativa { get; set; }

}
