---
name: exec-docs-keeper
description: >-
  Aciona o guardião de documentação (docs-keeper) para atualizar o checklist do
  PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md, diagrama ER (database-model.md), diagramas
  Mermaid, ADRs (docs/adr/), collection Postman e README.md após alterações no serviço.
---

# Skill `exec-docs-keeper` — Guardião de Documentação, Diagramas & ADRs

Esta skill aplica o protocolo do agente [`docs-keeper`](../../agents/docs-keeper.md).

## Procedimento

1. Leia [`/home/gabriel/me/SOAT/execution-service/.agents/agents/docs-keeper.md`](../../agents/docs-keeper.md).
2. Inspecione `git status` e `git diff` em `/home/gabriel/me/SOAT/execution-service` para identificar todas as mudanças realizadas na etapa atual.
3. Atualize os documentos impactados:
   - Checklist da Seção 11 em [`docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md)
   - Modelo de dados e índices em [`docs/database-model.md`](../../../docs/database-model.md)
   - Fluxos e arquitetura em [`docs/FLUXO_CRIACAO_ORDEM_SERVICO.md`](../../../docs/FLUXO_CRIACAO_ORDEM_SERVICO.md) e `docs/arquitetura.md`
   - ADRs em `docs/adr/` (`0001` a `0004`)
   - Collection Postman em `docs/postman/execution-service.postman_collection.json`
   - [`README.md`](../../../README.md)
