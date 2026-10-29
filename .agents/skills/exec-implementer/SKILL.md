---
name: exec-implementer
description: >-
  Aciona o agente implementador (implementer) para codificar casos de uso, queries,
  repositórios EF Core, controllers REST e configurações no Execution Service (.NET 8
  Clean Architecture). Use quando o usuário pedir para implementar código ou avançar uma
  etapa no execution-service.
---

# Skill `exec-implementer` — Implementação .NET 8 Clean Architecture

Esta skill aplica as diretrizes do agente [`implementer`](../../agents/implementer.md) para escrita de código de produção no `techchallenge-execution-service`.

## Procedimento

1. Leia as regras em [`/home/gabriel/me/SOAT/execution-service/.agents/agents/implementer.md`](../../agents/implementer.md) e [`/home/gabriel/me/SOAT/execution-service/AGENTS.md`](../../../AGENTS.md).
2. Implemente as alterações seguindo a ordem de dependência: `Domain` ➔ `Application` ➔ `Infrastructure` ➔ `Api`.
3. Caso deseje paralelizar ou isolar a implementação em subagente, utilize `define_subagent` / `invoke_subagent` com `Name: "implementer"` e `EnableWriteTools: true`.
4. Valide obrigatoriamente ao final com:
   ```bash
   cd /home/gabriel/me/SOAT/execution-service && dotnet build && dotnet test
   ```
   garantindo **`0 Warning(s)`** e **`0 Error(s)`**.
