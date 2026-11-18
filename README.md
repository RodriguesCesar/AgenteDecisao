# AgenteDecisao

API ASP.NET para tomada de decisão de inadimplência com suporte a LLM.

## Configuração segura

1. Não armazene segredos no repositório.
2. Configure a chave da OpenAI por variável de ambiente (PowerShell):

```powershell
$env:OpenAI__ApiKey="sua-chave-aqui"
```

3. Defina também uma API key para proteger o endpoint de decisão:

```powershell
$env:Authentication__AgenteApiKey="sua-chave-interna-da-api"
```

4. Alternativamente, use o arquivo local de exemplo e mantenha fora de versionamento:

- `AgendaDecisao.API/appsettings.Local.example.json`

## Executar localmente

```bash
dotnet run --project AgendaDecisao.API/AgenteDecisao.API.csproj
```

## Endpoints

- `GET /health`
- `POST /api/agente`

### Autenticação do endpoint

`POST /api/agente` exige o header `X-Agente-Api-Key`.

- Em `Development`, o bypass pode ser habilitado com `Authentication:AllowAnonymousInDevelopment=true`.
- Em outros ambientes, mantenha `AllowAnonymousInDevelopment=false` e configure `Authentication:AgenteApiKey`.

### Rate limiting

`POST /api/agente` possui limitação configurável:

- `RateLimiting:Agente:PermitLimit`
- `RateLimiting:Agente:WindowSeconds`
- `RateLimiting:Agente:QueueLimit`

Quando o limite é excedido, a API retorna `429 Too Many Requests`.

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

## Testes automatizados

Para executar a suíte de testes:

```bash
dotnet test AgenteDecisao.slnx
```

Projeto de testes:

- `AgendaDecisao.API.Tests`

Cobertura inicial incluída:

- decisão do agente aplicando ferramenta de cancelamento;
- `GET /health` com retorno de sucesso;
- `POST /api/agente` com payload válido;
- `POST /api/agente` com payload inválido (retorno `400`);
- `POST /api/agente` sem API key (retorno `401`);
- `POST /api/agente` excedendo limite de requisições (retorno `429`).

## CI (GitHub Actions)

Pipeline em `.github/workflows/ci.yml` executa em push/pull request:

- restore da solução;
- build em Release;
- execução dos testes automatizados.

## Melhorias aplicadas

### Fase 1

- remoção de segredo hardcoded do `appsettings.json`;
- validação de configuração crítica (`OpenAI:ApiKey`) no startup;
- validação de entrada com DataAnnotations;
- tratamento global de exceções com `ProblemDetails` e `traceId`;
- endpoint de health check (`/health`);
- propagação de `CancellationToken`;
- robustez no consumo da OpenAI (`EnsureSuccessStatusCode` + validação de payload);
- arquivo `.http` alinhado ao endpoint real.

### Fase 2

- criação do projeto `AgendaDecisao.API.Tests`;
- testes unitários para regra de decisão e aplicação de ferramenta;
- testes de integração para endpoints de health e decisão;
- pipeline de CI com restore/build/test.

### Fase 3

- autenticação por API key no endpoint de decisão;
- rate limiting configurável para `POST /api/agente`;
- testes de integração para cenários `401` e `429`;
- documentação operacional atualizada para autenticação e limites.

## Ação obrigatória pós-hardening

A chave exposta anteriormente no histórico do Git deve ser considerada comprometida.
Faça a rotação imediata no provedor e revogue a chave antiga.
