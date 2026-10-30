---
name: exec-phase-runner
description: >-
  Orquestra a execução ponta a ponta de uma etapa do PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md
  coordenando em sequência os agentes planner, implementer, saga-messaging-guardian,
  qa-bdd-tester e docs-keeper. Use quando o usuário pedir para implementar a próxima etapa
  do plano (ex.: Etapa 4, Etapa 5, Etapa 6, Etapa 7 ou Etapa 8).
---

# Skill `exec-phase-runner` — Orquestrador de Etapas do Execution Service

Esta skill padroniza a execução completa de qualquer etapa do [`PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md) coordenando os agentes especializados.

## Pipeline Padronizado de Execução

1. **Etapa A — Planejamento (`planner`):**
   - Carregue as diretrizes de [`/home/gabriel/me/SOAT/execution-service/.agents/agents/planner.md`](../../agents/planner.md).
   - Verifique na Seção 11 de `docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md` os itens pendentes `[ ]` da fase solicitada e liste os arquivos por camada.

2. **Etapa B — Implementação (`implementer` + `saga-messaging-guardian` / `devops-observability`):**
   - Siga [`/home/gabriel/me/SOAT/execution-service/.agents/agents/implementer.md`](../../agents/implementer.md) (e `saga-messaging-guardian.md` quando houver mensageria MassTransit, ou `devops-observability.md` quando houver K8s/CI/OTel).
   - Você pode delegar subtarefas isoladas invocando os subagentes correspondentes via `define_subagent` / `invoke_subagent`.

3. **Etapa C — Qualidade e Testes (`qa-bdd-tester`):**
   - Siga [`/home/gabriel/me/SOAT/execution-service/.agents/agents/qa-bdd-tester.md`](../../agents/qa-bdd-tester.md).
   - Crie/atualize os testes unitários, de integração (`Testcontainers`) e/ou BDD (`Reqnroll`).
   - Execute `dotnet test` em `/home/gabriel/me/SOAT/execution-service` garantindo `0 Warning(s)`, `0 Error(s)` e `LayerDependencyTests` verde.

4. **Etapa D — Documentação (`docs-keeper`):**
   - Siga [`/home/gabriel/me/SOAT/execution-service/.agents/agents/docs-keeper.md`](../../agents/docs-keeper.md).
   - Atualize obrigatoriamente [`docs/SPEC.md`](../../../docs/SPEC.md) refletindo o comportamento atual, diagramas Mermaid, novos endpoints/eventos e contexto da aplicação.
   - Marque os checkboxes `[x]` concluídos em `docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md` e atualize a documentação correspondente (`database-model.md`, ADRs, Postman, `README.md`).
