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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();

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

if (rateLimitSettings.PermitLimit < 1 || rateLimitSettings.WindowSeconds < 1)
{
    throw new InvalidOperationException("Configuração inválida em RateLimiting:Agente. Verifique PermitLimit e WindowSeconds.");
}

var rateLimitBuckets = new Dictionary<string, RateLimitBucket>(StringComparer.Ordinal);
var rateLimitLock = new object();

long authSuccessCount = 0;
long authFailureCount = 0;
long authConfigErrorCount = 0;
long rateLimitRejectionCount = 0;

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
var securityLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SecurityEvents");

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
    string rateLimitKey;

    if (app.Environment.IsDevelopment() && authSettings.AllowAnonymousInDevelopment)
    {
        Interlocked.Increment(ref authSuccessCount);
        rateLimitKey = BuildRateLimitKey(context, "development-bypass");
    }
    else
    {
        if (string.IsNullOrWhiteSpace(authSettings.AgenteApiKey))
        {
            Interlocked.Increment(ref authConfigErrorCount);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problemConfig = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Configuração de autenticação inválida.",
                Detail = "A chave de API obrigatória não foi configurada.",
                Instance = context.Request.Path
            };
            problemConfig.Extensions["traceId"] = context.TraceIdentifier;

            securityLogger.LogError(
                "Configuração inválida de autenticação no endpoint {Path}. TraceId={TraceId}",
                context.Request.Path,
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(problemConfig);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Agente-Api-Key", out var providedApiKey) ||
            string.IsNullOrWhiteSpace(providedApiKey))
        {
            Interlocked.Increment(ref authFailureCount);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var problemUnauthorized = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado.",
                Detail = "Header X-Agente-Api-Key não informado.",
                Instance = context.Request.Path
            };
            problemUnauthorized.Extensions["traceId"] = context.TraceIdentifier;

            securityLogger.LogWarning(
                "Falha de autenticação por header ausente em {Path}. TraceId={TraceId}",
                context.Request.Path,
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(problemUnauthorized);
            return;
        }

        if (!ApiKeysMatch(providedApiKey.ToString(), authSettings.AgenteApiKey))
        {
            Interlocked.Increment(ref authFailureCount);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var problemUnauthorized = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado.",
                Detail = "Chave de API inválida.",
                Instance = context.Request.Path
            };
            problemUnauthorized.Extensions["traceId"] = context.TraceIdentifier;

            securityLogger.LogWarning(
                "Falha de autenticação por chave inválida em {Path}. TraceId={TraceId}",
                context.Request.Path,
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(problemUnauthorized);
            return;
        }

        Interlocked.Increment(ref authSuccessCount);
        rateLimitKey = BuildRateLimitKey(context, BuildApiKeyIdentifier(providedApiKey.ToString()));
    }

    var now = DateTimeOffset.UtcNow;
    if (!TryConsumeRateLimitToken(rateLimitKey, now, out var retryAfterSeconds))
    {
        Interlocked.Increment(ref rateLimitRejectionCount);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        securityLogger.LogWarning(
            "Requisição bloqueada por rate limiting em {Path}. RateLimitKey={RateLimitKey} RetryAfterSeconds={RetryAfterSeconds} TraceId={TraceId}",
            context.Request.Path,
            rateLimitKey,
            retryAfterSeconds,
            context.TraceIdentifier);

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
app.MapGet("/metrics", () => Results.Text(BuildMetrics(), "text/plain; version=0.0.4; charset=utf-8"));
app.MapControllers();

app.Run();

string BuildMetrics()
{
    var sb = new StringBuilder();
    AppendCounter(sb, "agente_auth_success_total", "Total de autenticações válidas no endpoint /api/agente.", Interlocked.Read(ref authSuccessCount));
    AppendCounter(sb, "agente_auth_failures_total", "Total de falhas de autenticação no endpoint /api/agente.", Interlocked.Read(ref authFailureCount));
    AppendCounter(sb, "agente_auth_config_errors_total", "Total de erros de configuração de autenticação.", Interlocked.Read(ref authConfigErrorCount));
    AppendCounter(sb, "agente_rate_limit_rejections_total", "Total de rejeições por rate limiting no endpoint /api/agente.", Interlocked.Read(ref rateLimitRejectionCount));
    AppendCounter(sb, "agente_justificativa_fallback_total", "Total de respostas com fallback de justificativa por indisponibilidade da IA.", AgenteInadimpleciaDeClientes.JustificativaFallbackCount);
    return sb.ToString();
}

bool TryConsumeRateLimitToken(string key, DateTimeOffset now, out int retryAfterSeconds)
{
    lock (rateLimitLock)
    {
        if (!rateLimitBuckets.TryGetValue(key, out var bucket))
        {
            bucket = new RateLimitBucket(now, 0);
        }

        var elapsed = now - bucket.WindowStart;
        if (elapsed >= TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds))
        {
            bucket = new RateLimitBucket(now, 0);
            elapsed = TimeSpan.Zero;
        }

        if (bucket.RequestCount >= rateLimitSettings.PermitLimit)
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds) - elapsed).TotalSeconds));
            return false;
        }

        bucket = bucket with { RequestCount = bucket.RequestCount + 1 };
        rateLimitBuckets[key] = bucket;
        retryAfterSeconds = 0;
        return true;
    }
}

static void AppendCounter(StringBuilder sb, string name, string help, long value)
{
    sb.Append("# HELP ").Append(name).Append(' ').Append(help).AppendLine();
    sb.Append("# TYPE ").Append(name).Append(" counter").AppendLine();
    sb.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
}

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

static string BuildApiKeyIdentifier(string apiKey)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
    return Convert.ToHexString(hash[..6]);
}

static string BuildRateLimitKey(HttpContext context, string apiKeyIdentifier)
{
    var clientId = context.Request.Headers["X-Agente-Client-Id"].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(clientId))
    {
        if (clientId.Length > 64)
        {
            clientId = clientId[..64];
        }

        return $"cliente:{clientId}";
    }

    return $"apikey:{apiKeyIdentifier}";
}

public readonly record struct RateLimitBucket(DateTimeOffset WindowStart, int RequestCount);

public partial class Program;
