namespace AgenteDecisao.API.Settings;

public sealed class AgenteRateLimitingSettings
{
    public const string SectionName = "RateLimiting:Agente";

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int QueueLimit { get; set; }
}
