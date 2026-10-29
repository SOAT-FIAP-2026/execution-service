---
name: exec-saga-guardian
description: >-
  Aciona o guardião da Saga Coreografada e mensageria (saga-messaging-guardian) para
  implementar ou auditar contratos MassTransit 8.x, RabbitMQ, Transactional Outbox/Inbox,
  idempotência e fluxos de compensação entre OS Service, Billing Service e Execution Service.
---

# Skill `exec-saga-guardian` — Guardião da Saga Coreografada & MassTransit v8

Esta skill aplica as diretrizes do agente [`saga-messaging-guardian`](../../agents/saga-messaging-guardian.md).

## Procedimento

1. Leia [`/home/gabriel/me/SOAT/execution-service/.agents/agents/saga-messaging-guardian.md`](../../agents/saga-messaging-guardian.md) e a Seção 4 de [`/home/gabriel/me/SOAT/execution-service/docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md).
2. Verifique:
   - Versão do MassTransit fixada em `8.*` (Apache 2.0);
   - Identidade de namespace/URN (`Fiap.TechChallenge.Contracts.Events`) para todos os eventos publicados e consumidos;
   - Presença de `EventId`, `CorrelationId = osId` e `OccurredAt`;
   - Configuração de `AddEntityFrameworkOutbox<AppDbContext>()` (Outbox + Inbox no PostgreSQL);
   - Emissão idempotente de `ProducaoIniciadaEvent` (apenas na 1ª tarefa iniciada) e tratamento correto de `OrdemServicoCanceladaEvent` (Compensação A) e `ProducaoCanceladaEvent` (Compensação B).
