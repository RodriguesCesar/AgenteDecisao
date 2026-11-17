# AgenteDecisao

API ASP.NET para tomada de decisão de inadimplência com suporte a LLM.

## Configuração segura

1. Não armazene segredos no repositório.
2. Configure a chave da OpenAI por variável de ambiente (PowerShell):

```powershell
$env:OpenAI__ApiKey="sua-chave-aqui"
```

3. Alternativamente, use o arquivo local de exemplo e mantenha fora de versionamento:

- `AgendaDecisao.API/appsettings.Local.example.json`

## Executar localmente

```bash
dotnet run --project AgendaDecisao.API/AgenteDecisao.API.csproj
```

## Endpoints

- `GET /health`
- `POST /api/agente`

Exemplo de payload:

```json
{
  "entradaUsuario": "Cliente sinalizou dificuldade pontual de pagamento",
  "cliente": {
    "nome": "Cliente Exemplo",
    "diasInadimplente": 75
  },
  "assinatura": {
    "status": "ATIVA"
  }
}
```

## Melhorias aplicadas (fase 1)

- remoção de segredo hardcoded do `appsettings.json`;
- validação de configuração crítica (`OpenAI:ApiKey`) no startup;
- validação de entrada com DataAnnotations;
- tratamento global de exceções com `ProblemDetails` e `traceId`;
- endpoint de health check (`/health`);
- propagação de `CancellationToken`;
- robustez no consumo da OpenAI (`EnsureSuccessStatusCode` + validação de payload);
- arquivo `.http` alinhado ao endpoint real.

## Ação obrigatória pós-hardening

A chave exposta anteriormente no histórico do Git deve ser considerada comprometida.
Faça a rotação imediata no provedor e revogue a chave antiga.
