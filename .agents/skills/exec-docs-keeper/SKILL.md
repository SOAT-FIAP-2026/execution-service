---
name: exec-docs-keeper
description: >-
  Aciona o guardião de documentação (docs-keeper) para manter o SPEC.md atualizado com o contexto
  da aplicação, além do checklist do PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md, diagrama ER
  (database-model.md), diagramas Mermaid, ADRs (docs/adr/), collection Postman e README.md.
---

# Skill `exec-docs-keeper` — Guardião de Documentação, SPEC.md, Diagramas & ADRs

Esta skill aplica o protocolo do agente [`docs-keeper`](../../agents/docs-keeper.md).

## Procedimento

1. Leia [`/home/gabriel/me/SOAT/execution-service/.agents/agents/docs-keeper.md`](../../agents/docs-keeper.md).
2. Inspecione `git status` e `git diff` em `/home/gabriel/me/SOAT/execution-service` para identificar todas as mudanças realizadas na etapa atual.
3. Atualize obrigatoriamente os documentos impactados:
   - **Especificação Viva:** [`docs/SPEC.md`](../../../docs/SPEC.md) (e manter o symlink na raiz `SPEC.md`) atualizando o contexto da aplicação, diagramas Mermaid, matriz de endpoints e contratos de mensageria da Saga.
   - **Plano Executivo:** Checklist da Seção 11 em [`docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md)
   - **Modelo de Dados:** Modelo de dados e índices em [`docs/database-model.md`](../../../docs/database-model.md)
   - **Fluxos e Arquitetura:** [`docs/FLUXO_CRIACAO_ORDEM_SERVICO.md`](../../../docs/FLUXO_CRIACAO_ORDEM_SERVICO.md) e `docs/arquitetura.md`
   - **Decisões Arquiteturais:** ADRs em `docs/adr/` (`0001` a `0004`)
   - **Contratos e Testes:** Collection Postman em `docs/postman/execution-service.postman_collection.json`
   - **Guia do Repositório:** [`README.md`](../../../README.md)
