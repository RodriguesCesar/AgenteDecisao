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
- `GET /metrics`
- `POST /api/agente`

### Regras determinísticas de decisão 

A decisão final é calculada em código e **não depende da IA**:

- atraso `< 60` dias → `MANTER`
- atraso entre `60` e `120` dias (inclusive) → `SUSPENDER`
- atraso `> 120` dias → `CANCELAR`

A IA passa a ser usada apenas para gerar a **justificativa textual** da decisão.

### Resiliência de justificativa 

Se a IA ficar indisponível (erro, timeout ou resposta vazia), a API mantém a decisão determinística e retorna justificativa fallback:

`Decisão aplicada por regra de inadimplência; justificativa automática indisponível no momento.`

### Autenticação do endpoint

`POST /api/agente` exige o header `X-Agente-Api-Key`.

- Em `Development`, o bypass pode ser habilitado com `Authentication:AllowAnonymousInDevelopment=true`.
- Em outros ambientes, mantenha `AllowAnonymousInDevelopment=false` e configure `Authentication:AgenteApiKey`.

### Rate limiting

`POST /api/agente` possui limitação configurável em janela fixa:

- `RateLimiting:Agente:PermitLimit`
- `RateLimiting:Agente:WindowSeconds`

Quando o limite é excedido, a API retorna `429 Too Many Requests`.

A limitação é segmentada por chave derivada:

- `X-Agente-Client-Id` (quando informado);
- identificador seguro derivado da API key (quando `X-Agente-Client-Id` não é enviado).

### Observabilidade

A API registra eventos de segurança com `traceId` para:

- falha de autenticação por ausência de header;
- falha de autenticação por chave inválida;
- rejeição por rate limiting;
- erro de configuração de autenticação.

Também registra warnings quando ocorre fallback de justificativa por indisponibilidade da IA.

O endpoint `GET /metrics` expõe métricas no formato Prometheus:

- `agente_auth_success_total`
- `agente_auth_failures_total`
- `agente_auth_config_errors_total`
- `agente_rate_limit_rejections_total`
- `agente_justificativa_fallback_total`

Exemplo de consultas:

- `agente_auth_failures_total`
- `agente_rate_limit_rejections_total`
- `agente_justificativa_fallback_total`

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

- decisão determinística do agente para manter assinatura (`< 60`);
- decisão determinística do agente para suspender assinatura (`60-120`);
- decisão determinística do agente para cancelar assinatura (`> 120`);
- fallback de justificativa quando IA falha;
- `GET /health` com retorno de sucesso;
- `GET /metrics` com validação dos contadores;
- `POST /api/agente` com payload válido;
- `POST /api/agente` com payload inválido (retorno `400`);
- `POST /api/agente` sem API key (retorno `401`).

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
- testes de integração para cenário `401`;
- documentação operacional atualizada para autenticação e limites.

### Fase 4

- logs estruturados para eventos de segurança com `traceId`;
- endpoint `/metrics` no formato Prometheus;
- contadores de autenticação, erro de configuração e rate limiting;
- teste de integração para garantir exposição das métricas.

### Fase 5

- decisão final movida para regras determinísticas no código;
- IA utilizada apenas para justificativa da decisão;
- ampliação de testes unitários para cobrir faixas de atraso críticas.

### Fase 6

- fallback de justificativa para indisponibilidade da IA sem quebrar resposta de decisão;
- métrica `agente_justificativa_fallback_total` para observabilidade de fallback;
- rate limiting segmentado por cliente (`X-Agente-Client-Id`) ou identificador derivado da API key.

## Ação obrigatória pós-hardening

A chave exposta anteriormente no histórico do Git deve ser considerada comprometida.
Faça a rotação imediata no provedor e revogue a chave antiga.
