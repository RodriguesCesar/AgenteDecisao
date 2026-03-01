using AgenteDecisao.API.Agents;
using AgenteDecisao.API.Services;
using AgenteDecisao.API.Services.Interfaces;
using AgenteDecisao.API.Tools;
using AgenteDecisao.API.Tools.Intarfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//Injeções de dependências para os agentes e ferramentas:
builder.Services.AddHttpClient<IServicoIA, ServicoOpenIA>(client =>
{
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            builder.Configuration["OpenAI:ApiKey"]);
});

builder.Services.AddScoped<IFerramentaAgente, FerramentaSuspender>();
builder.Services.AddScoped<IFerramentaAgente, FerramentaCencelar>();

builder.Services.AddScoped<AgenteInadimpleciaDeClientes>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
