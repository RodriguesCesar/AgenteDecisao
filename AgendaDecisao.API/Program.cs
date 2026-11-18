using AgenteDecisao.API.Agents;
using AgenteDecisao.API.Services;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Settings;
using AgenteDecisao.API.Tools;
using AgenteDecisao.API.Tools.Intarfaces;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<OpenAISettings>()
    .Bind(builder.Configuration.GetSection(OpenAISettings.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ApiKey), "OpenAI:ApiKey precisa ser configurada.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthenticationSettings>()
    .Bind(builder.Configuration.GetSection(AuthenticationSettings.SectionName))
    .Validate(settings => settings.AllowAnonymousInDevelopment || !string.IsNullOrWhiteSpace(settings.AgenteApiKey),
        "Authentication:AgenteApiKey precisa ser configurada quando o bypass em Development estiver desabilitado.")
    .ValidateOnStart();

var rateLimitSettings = builder.Configuration
    .GetSection(AgenteRateLimitingSettings.SectionName)
    .Get<AgenteRateLimitingSettings>() ?? new AgenteRateLimitingSettings();

if (rateLimitSettings.PermitLimit < 1 || rateLimitSettings.WindowSeconds < 1 || rateLimitSettings.QueueLimit < 0)
{
    throw new InvalidOperationException("Configuração inválida em RateLimiting:Agente. Verifique PermitLimit, WindowSeconds e QueueLimit.");
}

var agenteLimiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
{
    PermitLimit = rateLimitSettings.PermitLimit,
    Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds),
    QueueLimit = rateLimitSettings.QueueLimit,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    AutoReplenishment = true
});

builder.Services.AddHttpClient<IServicoIA, ServicoOpenIA>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<OpenAISettings>>().Value;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IFerramentaAgente, FerramentaSuspender>();
builder.Services.AddScoped<IFerramentaAgente, FerramentaCencelar>();
builder.Services.AddScoped<AgenteInadimpleciaDeClientes>();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("GlobalExceptionHandler");
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is not null)
        {
            logger.LogError(exception, "Erro não tratado durante o processamento da requisição.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erro interno no servidor.",
            Detail = "Ocorreu um erro inesperado ao processar a requisição.",
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem);
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();

app.Use(async (context, next) =>
{
    if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.Equals("/api/agente", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var authSettings = context.RequestServices.GetRequiredService<IOptions<AuthenticationSettings>>().Value;

    if (app.Environment.IsDevelopment() && authSettings.AllowAnonymousInDevelopment)
    {
        await next();
        return;
    }

    if (string.IsNullOrWhiteSpace(authSettings.AgenteApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problemConfig = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Configuração de autenticação inválida.",
            Detail = "A chave de API obrigatória não foi configurada.",
            Instance = context.Request.Path
        };
        problemConfig.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problemConfig);
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-Agente-Api-Key", out var providedApiKey) ||
        string.IsNullOrWhiteSpace(providedApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var problemUnauthorized = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autorizado.",
            Detail = "Header X-Agente-Api-Key não informado.",
            Instance = context.Request.Path
        };
        problemUnauthorized.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problemUnauthorized);
        return;
    }

    if (!ApiKeysMatch(providedApiKey.ToString(), authSettings.AgenteApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var problemUnauthorized = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autorizado.",
            Detail = "Chave de API inválida.",
            Instance = context.Request.Path
        };
        problemUnauthorized.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problemUnauthorized);
        return;
    }

    using var lease = await agenteLimiter.AcquireAsync(1, context.RequestAborted);
    if (!lease.IsAcquired)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var rateProblem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Limite de requisições excedido.",
            Detail = "Tente novamente em alguns instantes.",
            Instance = context.Request.Path
        };
        rateProblem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(rateProblem);
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

static bool ApiKeysMatch(string provided, string expected)
{
    var providedBytes = Encoding.UTF8.GetBytes(provided);
    var expectedBytes = Encoding.UTF8.GetBytes(expected);

    if (providedBytes.Length != expectedBytes.Length)
    {
        return false;
    }

    return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}

public partial class Program;
