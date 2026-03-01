namespace AgenteDecisao.API.Services.Interfaces;

/// <summary>
/// Contrato para serviços de Inteligência Artificial (IA) 
/// que podem ser utilizados pelo agente de decisão.
/// </summary>
public interface IServicoIA
{
    /// <summary>
    /// Executa uma operação assíncrona baseada no texto fornecido 
    /// e retorna o resultado como uma cadeia de caracteres.
    /// </summary>        
    Task<string> ExecutarAsync(string prompt);
}
