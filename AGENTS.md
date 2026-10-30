# 🔧 Execution & Production Service (`techchallenge-execution-service`) — Diretrizes e Protocolo de Agentes

Este arquivo define as regras arquiteturais sempre ativas e o protocolo padronizado de agentes para o desenvolvimento, teste e documentação do microsserviço de **Execução & Produção (Fase 4 — SOAT FIAP)**.

Referência Mestre: [`docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](./docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md)

---

## 1. Invariantes Arquiteturais (Clean Architecture + DDD)

1. **Dependências entre Camadas (validadas por `LayerDependencyTests`):**
   - `Fiap.TechChallenge.Execution.Domain`: **Zero** dependências de outras camadas ou de frameworks (`Microsoft.EntityFrameworkCore`, `MassTransit`, `Microsoft.AspNetCore`).
   - `Fiap.TechChallenge.Execution.Application`: Depende apenas de `Domain` (e `Fiap.TechChallenge.Contracts`). **Proibido** referenciar `Infrastructure`, `Api`, `Microsoft.EntityFrameworkCore` ou `Microsoft.AspNetCore`.
   - `Fiap.TechChallenge.Execution.Infrastructure`: Implementa interfaces de `Domain` e `Application` (PostgreSQL 16 + EF Core 8, MassTransit v8, AWS SSM). **Proibido** referenciar `Api`.
   - `Fiap.TechChallenge.Execution.Api`: Camada de composição, Controllers REST (`/api/v1/producao`), autenticação JWT de staff, middlewares (`ProblemDetails`, `CorrelationId`) e observabilidade.

2. **Domínio & Máquina de Estados (`OrdemProducao`):**
   - Estados persistidos e trafegados em `UPPER_SNAKE`: `AGUARDANDO_DIAGNOSTICO` ➔ `EM_DIAGNOSTICO` ➔ `DIAGNOSTICO_CONCLUIDO` ➔ `NA_FILA` ➔ `EM_EXECUCAO` ➔ `CONCLUIDA` (ou `CANCELADA` em qualquer etapa prévia à conclusão).
   - Estados de `TarefaProducao`: `PENDENTE`, `EM_EXECUCAO`, `CONCLUIDA`, `INTERROMPIDA`.
   - **LGPD:** `ClienteSnapshot` armazena apenas `IdCliente`, `Nome` e `Telefone`. CPF/CNPJ e e-mail jamais entram neste serviço.
   - **Exceções de Domínio Tipadas:**
     - `TransicaoEstadoInvalidaException` ➔ HTTP `409 Conflict`
     - `ConflitoConcorrenciaException` ➔ HTTP `409 Conflict`
     - `MecanicoNaoAutorizadoException` ➔ HTTP `403 Forbidden`
     - `TarefaProducaoNaoEncontradaException` / Ordem inexistente ➔ HTTP `404 Not Found`
     - `DomainException` (validação geral) ➔ HTTP `400 Bad Request`

3. **Persistência Relacional (`PostgreSQL 16` + `EF Core 8`):**
   - Banco exclusivo: `techchallenge_execution_db` (nenhum acesso direto ao banco do OS Service ou Billing Service).
   - Identificadores `Guid` gerados no domínio com `.ValueGeneratedNever()` no EF Core.
   - Value Objects (`ClienteSnapshot`, `VeiculoSnapshot`) mapeados via `.ComplexProperty(...)`.
   - Concorrência otimista obrigatória via coluna `versao` (`IsConcurrencyToken()`) incrementada em `OrdemProducaoRepository.AtualizarAsync`.
   - Consultas de leitura do agregado completo utilizam `.AsSplitQuery()`.

4. **Mensageria & Saga Coreografada (`MassTransit 8.*` + `RabbitMQ 3.13`):**
   - **Versão do MassTransit:** Fixar sempre em `8.*` (Apache 2.0). **Nunca** atualizar para `v9` (licença comercial).
   - **Consistência de URN:** Eventos de integração residem no namespace `Fiap.TechChallenge.Contracts.Events` (com `EventId`, `CorrelationId = osId`, `OccurredAt`).
   - **Confiabilidade:** Transactional Outbox e Inbox do EF Core (`AddEntityFrameworkOutbox<AppDbContext>()`), retry exponencial e Dead-Letter Queue (`_error`).
   - **Regra de Cancelamento:** A Execução consome cancelamentos externos exclusivamente do OS Service (`OrdemServicoCanceladaEvent`) e publica `ProducaoCanceladaEvent` quando o cancelamento parte da oficina.

5. **Segurança e Autenticação:**
   - Stateless via JWT simétrico (HMAC-SHA256) emitido pelo OS Service (`/techchallenge/prod/jwt_secret`, `Issuer = fiap-tech-challenge`, `Audience = fiap-api`).
   - Identificação do mecânico executor via claim `UsuarioId` (`Guid`) e nome via `ClaimTypes.Name`.
   - Autorização por roles: `Mecanico`, `Operador`, `Administrador`. Tokens de cliente (Lambda/CPF) não possuem role e são bloqueados.

---

## 2. Catálogo de Agentes Especializados

As definições completas de prompt ficam em [`.agents/agents/`](./.agents/agents/) e as skills acionáveis em [`.agents/skills/`](./.agents/skills/):

| Agente | Definição | Skill Acionável | Papel |
|---|---|---|---|
| **`planner`** | [`.agents/agents/planner.md`](./.agents/agents/planner.md) | `exec-planner` | Decompõe etapas do plano em tarefas por camada, valida pré-requisitos e contratos entre microsserviços. |
| **`implementer`** | [`.agents/agents/implementer.md`](./.agents/agents/implementer.md) | `exec-implementer` | Implementa código enxuto em .NET 8 respeitando Clean Architecture, DDD, EF Core 8 e `0 Warning(s)`. |
| **`saga-messaging-guardian`** | [`.agents/agents/saga-messaging-guardian.md`](./.agents/agents/saga-messaging-guardian.md) | `exec-saga-guardian` | Audita e implementa contratos MassTransit v8, URNs, Outbox/Inbox, idempotência e compensações da Saga. |
| **`qa-bdd-tester`** | [`.agents/agents/qa-bdd-tester.md`](./.agents/agents/qa-bdd-tester.md) | `exec-qa-bdd` | Constrói e executa testes unitários, integração (`Testcontainers`) e BDD (`Reqnroll`), garantindo cobertura ≥ 80%. |
| **`docs-keeper`** | [`.agents/agents/docs-keeper.md`](./.agents/agents/docs-keeper.md) | `exec-docs-keeper` | Mantém sincronizados `docs/SPEC.md` (especificação viva e contexto), `PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`, `database-model.md`, ADRs, Postman e `README.md`. |
| **`devops-observability`** | [`.agents/agents/devops-observability.md`](./.agents/agents/devops-observability.md) | `exec-devops-otel` | Gerencia Kustomize (`NodePort 30082`), CI/CD GitHub Actions (OIDC), SSM e OpenTelemetry (Prometheus/Loki/Tempo). |

---

## 3. Protocolo Padrão de Execução de Etapas (`exec-phase-runner`)

Ao iniciar qualquer etapa do roadmap (Etapas 4 a 8):

1. **Fase 1 — Planejamento (`planner`):**
   - Ler a seção correspondente em `docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md` e listar os arquivos a criar/modificar por camada (`Domain` ➔ `Application` ➔ `Infrastructure` ➔ `Api` ➔ `Tests` ➔ `Docs`).
2. **Fase 2 — Implementação (`implementer` + `saga-messaging-guardian`):**
   - Implementar casos de uso, consumidores/publicadores, repositórios ou endpoints mantendo `dotnet build` com `0 Warning(s)` e `0 Error(s)`.
   - Quando houver eventos ou compensações, validar URNs, `CorrelationId = osId`, Outbox/Inbox e idempotência com `saga-messaging-guardian`.
3. **Fase 3 — Verificação & Qualidade (`qa-bdd-tester`):**
   - Criar/atualizar testes unitários, de integração com Testcontainers e/ou BDD Reqnroll.
   - Executar `dotnet test` garantindo 100% de aprovação nos testes de unidade, integração e `LayerDependencyTests`.
4. **Fase 4 — Sincronização Documental (`docs-keeper`):**
   - Atualizar obrigatoriamente [`docs/SPEC.md`](./docs/SPEC.md) refletindo o comportamento atual, diagramas Mermaid, novas rotas, eventos e contexto de negócio da aplicação.
   - Marcar os itens concluídos `[x]` no checklist de `docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md` (Seção 11) e atualizar diagramas, ADRs, collection Postman ou `README.md` afetados pela entrega.
