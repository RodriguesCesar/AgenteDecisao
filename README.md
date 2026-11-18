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
- `POST /api/agente` com payload inválido (retorno `400`).

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

## Ação obrigatória pós-hardening

A chave exposta anteriormente no histórico do Git deve ser considerada comprometida.
Faça a rotação imediata no provedor e revogue a chave antiga.
