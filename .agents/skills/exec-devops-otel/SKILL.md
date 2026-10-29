---
name: exec-devops-otel
description: >-
  Aciona o especialista em DevOps, Kubernetes/Kustomize, CI/CD GitHub Actions (OIDC + SonarCloud)
  e Observabilidade OpenTelemetry (Prometheus, Loki, Tempo) para o Execution Service.
---

# Skill `exec-devops-otel` — Infraestrutura K8s, CI/CD & Observabilidade OpenTelemetry

Esta skill aplica as diretrizes do agente [`devops-observability`](../../agents/devops-observability.md).

## Procedimento

1. Leia [`/home/gabriel/me/SOAT/execution-service/.agents/agents/devops-observability.md`](../../agents/devops-observability.md) e a Seção 9 de [`/home/gabriel/me/SOAT/execution-service/docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md`](../../../docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md).
2. Aplique os padrões de infraestrutura e observabilidade do ecossistema SOAT:
   - Kustomize (`k8s/base` e `k8s/overlays/aws` com `NodePort 30082`);
   - Parâmetros AWS SSM Parameter Store (`/techchallenge/prod/execution/db_connection_string`, `/techchallenge/prod/rabbitmq_connection_string`, `/techchallenge/prod/jwt_secret`);
   - GitHub Actions (`ci.yml` e `cd.yml` com actions pinadas por commit SHA e deploy via OIDC);
   - OpenTelemetry (`AddAspNetCoreInstrumentation`, `AddNpgsql`, `AddEntityFrameworkCoreInstrumentation`, `AddSource("MassTransit")`, métricas customizadas Prometheus e probes `/health/live` e `/health/ready`).
