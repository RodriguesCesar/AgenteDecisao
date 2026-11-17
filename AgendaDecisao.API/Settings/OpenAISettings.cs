namespace AgenteDecisao.API.Settings;

public sealed class OpenAISettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
}
