namespace AgenteDecisao.API.Settings;

public sealed class AuthenticationSettings
{
    public const string SectionName = "Authentication";

    public string AgenteApiKey { get; set; } = string.Empty;

    public bool AllowAnonymousInDevelopment { get; set; }
}
