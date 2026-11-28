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
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Model), "OpenAI:Model precisa ser configurado.")
    .Validate(settings => settings.TimeoutSeconds is >= 5 and <= 120, "OpenAI:TimeoutSeconds deve estar entre 5 e 120 segundos.")
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

if (rateLimitSettings.PermitLimit < 1 || rateLimitSettings.WindowSeconds < 1 || rateLimitSettings.MaxBuckets < 1 || rateLimitSettings.CleanupBatchSize < 1)
{
    throw new InvalidOperationException("Configuração inválida em RateLimiting:Agente. Verifique PermitLimit, WindowSeconds, MaxBuckets e CleanupBatchSize.");
}

var rateLimitBuckets = new Dictionary<string, RateLimitBucket>(StringComparer.Ordinal);
var rateLimitLock = new object();

long authSuccessCount = 0;
long authFailureCount = 0;
long authConfigErrorCount = 0;
long rateLimitRejectionCount = 0;
long rateLimitCapacityRejectionCount = 0;
long rateLimitInvalidClientIdCount = 0;

builder.Services.AddHttpClient<IServicoIA, ServicoOpenIA>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<OpenAISettings>>().Value;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
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
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    await next();
});

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
        rateLimitKey = BuildRateLimitKey(context, "development-bypass", out var invalidClientId);

        if (invalidClientId)
        {
            Interlocked.Increment(ref rateLimitInvalidClientIdCount);
            await WriteInvalidClientIdProblemAsync(context, securityLogger);
            return;
        }
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
        rateLimitKey = BuildRateLimitKey(context, BuildApiKeyIdentifier(providedApiKey.ToString()), out var invalidClientId);

        if (invalidClientId)
        {
            Interlocked.Increment(ref rateLimitInvalidClientIdCount);
            await WriteInvalidClientIdProblemAsync(context, securityLogger);
            return;
        }
    }

    var now = DateTimeOffset.UtcNow;
    var rateLimitDecision = TryConsumeRateLimitToken(rateLimitKey, now, out var retryAfterSeconds);
    if (rateLimitDecision is not RateLimitDecision.Allowed)
    {
        Interlocked.Increment(ref rateLimitRejectionCount);

        if (rateLimitDecision is RateLimitDecision.CapacityExceeded)
        {
            Interlocked.Increment(ref rateLimitCapacityRejectionCount);
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var detail = rateLimitDecision is RateLimitDecision.CapacityExceeded
            ? "Capacidade de controle de rate limiting excedida temporariamente. Tente novamente em alguns instantes."
            : "Tente novamente em alguns instantes.";

        securityLogger.LogWarning(
            "Requisição bloqueada por rate limiting em {Path}. RateLimitKey={RateLimitKey} Motivo={Motivo} RetryAfterSeconds={RetryAfterSeconds} TraceId={TraceId}",
            context.Request.Path,
            rateLimitKey,
            rateLimitDecision,
            retryAfterSeconds,
            context.TraceIdentifier);

        var rateProblem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Limite de requisições excedido.",
            Detail = detail,
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
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
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
    AppendCounter(sb, "agente_rate_limit_capacity_rejections_total", "Total de rejeições por limite de capacidade de buckets do rate limiting.", Interlocked.Read(ref rateLimitCapacityRejectionCount));
    AppendCounter(sb, "agente_rate_limit_invalid_client_id_total", "Total de requisições rejeitadas por X-Agente-Client-Id inválido.", Interlocked.Read(ref rateLimitInvalidClientIdCount));
    AppendCounter(sb, "agente_justificativa_fallback_total", "Total de respostas com fallback de justificativa por indisponibilidade da IA.", AgenteInadimpleciaDeClientes.JustificativaFallbackCount);
    return sb.ToString();
}

RateLimitDecision TryConsumeRateLimitToken(string key, DateTimeOffset now, out int retryAfterSeconds)
{
    lock (rateLimitLock)
    {
        CleanupExpiredBuckets(now);

        if (!rateLimitBuckets.TryGetValue(key, out var bucket))
        {
            if (rateLimitBuckets.Count >= rateLimitSettings.MaxBuckets)
            {
                retryAfterSeconds = Math.Max(1, rateLimitSettings.WindowSeconds);
                return RateLimitDecision.CapacityExceeded;
            }

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
            return RateLimitDecision.WindowLimitExceeded;
        }

        bucket = bucket with { RequestCount = bucket.RequestCount + 1 };
        rateLimitBuckets[key] = bucket;
        retryAfterSeconds = 0;
        return RateLimitDecision.Allowed;
    }
}

void CleanupExpiredBuckets(DateTimeOffset now)
{
    var cutoff = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds);
    var keysToRemove = new List<string>(rateLimitSettings.CleanupBatchSize);

    foreach (var pair in rateLimitBuckets)
    {
        if (now - pair.Value.WindowStart >= cutoff)
        {
            keysToRemove.Add(pair.Key);
            if (keysToRemove.Count >= rateLimitSettings.CleanupBatchSize)
            {
                break;
            }
        }
    }

    foreach (var key in keysToRemove)
    {
        rateLimitBuckets.Remove(key);
    }
}

async Task WriteInvalidClientIdProblemAsync(HttpContext context, ILogger logger)
{
    context.Response.StatusCode = StatusCodes.Status400BadRequest;

    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Header inválido.",
        Detail = "O header X-Agente-Client-Id deve conter apenas letras, números, ponto, hífen ou underscore, com até 64 caracteres.",
        Instance = context.Request.Path
    };
    problem.Extensions["traceId"] = context.TraceIdentifier;

    logger.LogWarning(
        "Requisição rejeitada por X-Agente-Client-Id inválido em {Path}. TraceId={TraceId}",
        context.Request.Path,
        context.TraceIdentifier);

    await context.Response.WriteAsJsonAsync(problem);
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

static string BuildRateLimitKey(HttpContext context, string apiKeyIdentifier, out bool invalidClientId)
{
    invalidClientId = false;

    var clientId = context.Request.Headers["X-Agente-Client-Id"].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(clientId))
    {
        if (!IsValidClientId(clientId))
        {
            invalidClientId = true;
            return string.Empty;
        }

        return $"cliente:{clientId}";
    }

    return $"apikey:{apiKeyIdentifier}";
}

static bool IsValidClientId(string value)
{
    if (value.Length > 64)
    {
        return false;
    }

    foreach (var ch in value)
    {
        if (!(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_'))
        {
            return false;
        }
    }

    return true;
}

public readonly record struct RateLimitBucket(DateTimeOffset WindowStart, int RequestCount);

public enum RateLimitDecision
{
    Allowed,
    WindowLimitExceeded,
    CapacityExceeded
}

public partial class Program;
