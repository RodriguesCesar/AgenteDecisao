namespace AgenteDecisao.API.Settings;

public sealed class OpenAISettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o";

    public int TimeoutSeconds { get; set; } = 30;
}
