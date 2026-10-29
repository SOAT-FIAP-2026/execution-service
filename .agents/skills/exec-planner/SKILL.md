---
name: exec-planner
description: >-
  Aciona o agente planejador (planner) para analisar e decompor qualquer etapa do
  PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md em tarefas técnicas ordenadas por camada
  antes da implementação. Use quando o usuário pedir para planejar uma etapa ou feature
  do Execution Service.
---

# Skill `exec-planner` — Planejamento Técnico de Etapas (Execution Service)

Esta skill aplica o protocolo do agente [`planner`](../../agents/planner.md) para planejar entregas no `techchallenge-execution-service`.

## Procedimento

1. Leia a definição completa do agente em [`/home/gabriel/me/SOAT/execution-service/.agents/agents/planner.md`](../../agents/planner.md).
2. Consulte o checklist atual em [`/home/gabriel/me/SOAT/execution-service/docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md) (Seção 11) e as regras em [`/home/gabriel/me/SOAT/execution-service/AGENTS.md`](../../../AGENTS.md).
3. Para delegar a investigação em contexto isolado, registre/invoque o subagente `planner` (`EnableWriteTools: false`) usando o conteúdo de `.agents/agents/planner.md` como `SystemPrompt`.
4. Entregue o plano detalhado por camada (`Domain` ➔ `Application` ➔ `Infrastructure` ➔ `Api` ➔ `Tests` ➔ `Docs`) com critérios claros de verificação.
