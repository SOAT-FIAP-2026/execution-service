---
name: exec-qa-bdd
description: >-
  Aciona o especialista em qualidade e testes (qa-bdd-tester) para criar e executar
  testes unitários (xUnit), testes de integração (Testcontainers PostgreSQL 16 e RabbitMQ),
  testes BDD em português (Reqnroll/Gherkin) e validar cobertura >= 80% no Execution Service.
---

# Skill `exec-qa-bdd` — Qualidade, Testcontainers, BDD & Cobertura (≥ 80%)

Esta skill aplica as diretrizes do agente [`qa-bdd-tester`](../../agents/qa-bdd-tester.md).

## Procedimento

1. Leia [`/home/gabriel/me/SOAT/execution-service/.agents/agents/qa-bdd-tester.md`](../../agents/qa-bdd-tester.md) e a Seção 8 de [`/home/gabriel/me/SOAT/execution-service/docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md).
2. Certifique-se de que toda nova regra de domínio, caso de uso, consumer, repositório ou controller possua cobertura de testes unitários e/ou integração com Testcontainers (`postgres:16-alpine`, `rabbitmq:3.13-alpine`) e BDD (`Reqnroll`).
3. Execute a suíte completa e coleta de cobertura:
   ```bash
   cd /home/gabriel/me/SOAT/execution-service && dotnet test --collect:"XPlat Code Coverage"
   ```
4. Confirme que `LayerDependencyTests` e todos os testes passam sem falhas e com cobertura ≥ 80%.
