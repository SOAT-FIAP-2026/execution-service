# 📐 Fluxo de Criação da Ordem de Serviço (OS) & Inicialização da Produção
**Pós-Graduação em Software Architecture (SOAT) — FIAP**  
**Contexto:** Desacoplamento entre `fase1-tech-challenge` (OS Service) e `techchallenge-execution-service` (Execution Service)  
**Referência:** [PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md](file:///home/gabriel/Desktop/FIAP%20-%20TC/execution-service/docs/PLANO_DESACOPLAMENTO_SERVICO_EXECUCAO.md)

---

## 1. Visão Geral

Este documento descreve o fluxo detalhado de criação de uma Ordem de Serviço (OS) no balcão da oficina e a consequente inicialização assíncrona da Ordem de Produção no chão de oficina.

O fluxo garante o desacoplamento estrito entre os domínios:
1. **OS Service (`fase1-tech-challenge`)**: Dono cadastral do cliente, veículo e da Ordem de Serviço macro (status `RECEBIDA`).
2. **Broker de Mensageria (RabbitMQ)**: Transporte assíncrono e resiliente via evento de integração `OrdemServicoAbertaEvent`.
3. **Execution Service (`techchallenge-execution-service`)**: Dono da operação física, criando a `OrdemProducao` (status `AGUARDANDO_DIAGNOSTICO`) com snapshots imutáveis em seu próprio banco de dados relacional isolado.

---

## 2. Diagrama de Sequência — Criação da OS e Inicialização da Produção

```mermaid
sequenceDiagram
    autonumber
    actor A as 👤 Atendente / Balcão
    participant GW as 🚪 API Gateway / ALB
    participant OS as ⚙️ OS Service (fase1)
    participant DB_OS as 🗄️ RDS PostgreSQL (OS DB)
    participant BUS as 📬 RabbitMQ
    participant EXEC as 🔧 Execution Service
    participant DB_EX as 🗄️ PostgreSQL (Exec DB)
    actor M as 👨‍🔧 Mecânico (Oficina)

    %% 1. Requisição de Abertura
    A->>GW: POST /api/v1/ordens-servico<br/>{ clienteId, veiculoId, observacao, filialId }
    GW->>OS: Forward da requisição (JWT autenticado)

    %% 2. Validação e Persistência no OS Service
    activate OS
    OS->>DB_OS: Valida existência de Cliente e Veículo
    DB_OS-->>OS: Dados cadastrais válidos
    OS->>OS: Instancia OrdemServico (Status = RECEBIDA)
    
    %% 3. Transação com Outbox Pattern
    rect rgb(240, 248, 255)
        Note over OS,DB_OS: Transação Atômica com Outbox Pattern
        OS->>DB_OS: INSERT INTO ordens_servico (...)
        OS->>DB_OS: INSERT INTO outbox_messages (OrdemServicoAbertaEvent)
        DB_OS-->>OS: Commit confirmado
    end

    %% 4. Resposta Síncrona imediata para o Balcão
    OS-->>GW: 201 Created { id: "osId", status: "Recebida" }
    GW-->>A: 201 Created (OS aberta no sistema)
    deactivate OS

    %% 5. Publicação Assíncrona no Broker
    OS->>BUS: Publish OrdemServicoAbertaEvent<br/>{ osId, clienteSnapshot, veiculoSnapshot, filialId }

    %% 6. Consumo e Inicialização na Execução
    activate EXEC
    BUS->>EXEC: Consume OrdemServicoAbertaEvent (via MassTransit)
    
    rect rgb(255, 250, 240)
        Note over EXEC,DB_EX: Contexto de Chão de Oficina
        EXEC->>EXEC: Cria Agregado OrdemProducao<br/>(Status = AGUARDANDO_DIAGNOSTICO)
        EXEC->>DB_EX: INSERT INTO ordens_producao (...)
        DB_EX-->>EXEC: Registro confirmado
    end
    deactivate EXEC

    %% 7. Disponibilização Operacional
    M->>EXEC: GET /api/v1/producao/diagnosticos/pendentes
    EXEC-->>M: 200 OK [Ordens disponíveis para início de diagnóstico]
```

---

## 3. Diagrama de Arquitetura e Bounded Contexts

```mermaid
flowchart TD
    subgraph INGRESS["🚪 Camada de Entrada"]
        ClientReq["👤 Atendente / Balcão\nPOST /api/v1/ordens-servico"]
        ALB["AWS Application Load Balancer\nRegra: /api/v1/ordens-servico* ➔ TG 30080"]
        ClientReq --> ALB
    end

    subgraph BC_OS["📦 Bounded Context: Ordem de Serviço (fase1-tech-challenge)"]
        OS_API["OrdemServicoController"]
        OS_SVC["OrdemServicoService"]
        DB_OS[("🗄️ RDS PostgreSQL 16\nDatabase: techchallenge_os_db\n- clientes\n- veiculos\n- ordens_servico\n- outbox_messages")]
        
        ALB --> OS_API
        OS_API --> OS_SVC
        OS_SVC --> DB_OS
    end

    subgraph BROKER["📬 Mensageria Assíncrona (RabbitMQ no EKS)"]
        Exchange["Exchange: Fiap.TechChallenge.Contracts:OrdemServicoAbertaEvent\n(Topic / Fanout)"]
        Queue["Queue: execution-ordem-servico-aberta\n(Durable, Dead-Letter Exchange habilitada)"]
        Exchange --> Queue
    end

    subgraph BC_EXEC["🔧 Bounded Context: Chão de Oficina (techchallenge-execution-service)"]
        Consumer["OrdemServicoAbertaConsumer\n(MassTransit 8.x + Inbox Pattern)"]
        ExecDomain["Agregado OrdemProducao\nStatus: AGUARDANDO_DIAGNOSTICO"]
        DB_EXEC[("🗄️ PostgreSQL 16 Dedicado\nDatabase: techchallenge_execution_db\n- ordens_producao\n- historico_producao\n- snapshots (cliente/veiculo)")]
        ExecAPI["ProducaoController\nGET /api/v1/producao/diagnosticos/pendentes"]
        Mecanico["👨‍🔧 Mecânico (App / Terminal Oficina)"]

        Queue --> Consumer
        Consumer --> ExecDomain
        ExecDomain --> DB_EXEC
        DB_EXEC --> ExecAPI
        Mecanico --> ExecAPI
    end

    OS_SVC -.->|1. Grava Outbox e Dispara| Exchange
```

---

## 4. Máquina de Estados da Transição Inicial

```mermaid
stateDiagram-v2
    direction LR

    state "OS Service (fase1)" as OSContext {
        [*] --> RECEBIDA : POST /api/v1/ordens-servico
        RECEBIDA --> EM_DIAGNOSTICO : DiagnosticoIniciadoEvent
    }

    state "Execution Service" as ExecContext {
        [*] --> AGUARDANDO_DIAGNOSTICO : OrdemServicoAbertaEvent
        AGUARDANDO_DIAGNOSTICO --> EM_DIAGNOSTICO : PATCH /producao/{id}/diagnostico/iniciar
    }

    RECEBIDA -.->|OrdemServicoAbertaEvent| AGUARDANDO_DIAGNOSTICO
```

---

## 5. Especificação dos Contratos de Dados

### 5.1. Requisição HTTP de Criação (OS Service)
* **Endpoint:** `POST /api/v1/ordens-servico`
* **Autenticação:** `Bearer {JWT}` (Role: `Administrador` ou `Operador`)
* **Payload:**
```json
{
  "clienteId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "veiculoId": "f1e2d3c4-b5a6-9788-7654-3210fedcba98",
  "observacao": "Cliente relata ruído estranho na suspensão dianteira ao frear.",
  "filialId": "sp-matriz"
}
```

### 5.2. Evento de Integração `OrdemServicoAbertaEvent` (MassTransit / RabbitMQ)
* **Contrato:** `Fiap.TechChallenge.Contracts.Events.OrdemServicoAbertaEvent`
* **Payload:**
```json
{
  "eventId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "occurredAt": "2026-10-26T23:30:00Z",
  "osId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "filialId": "sp-matriz",
  "cliente": {
    "idCliente": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
    "nome": "João da Silva",
    "telefone": "+5511999998888"
  },
  "veiculo": {
    "placa": "ABC1D23",
    "modelo": "Honda Civic Touring 1.5",
    "ano": 2022
  }
}
```

> [!IMPORTANT]
> **Privacidade & LGPD:**
> Dados sensíveis desnecessários para a atividade do mecânico (como CPF/CNPJ e e-mail) **não** trafegam no payload do evento e não são persistidos no Execution Service. O mecânico recebe apenas o nome e telefone para comunicação operacional de oficina.

---

## 6. Padrões de Resiliência Aplicados

1. **Transactional Outbox Pattern (no OS Service):**
   - Garante que a abertura da OS e a emissão do evento ocorram na mesma transação ACID do PostgreSQL.
   - Elimina cenários de inconsistência ("OS criada mas evento não publicado" ou vice-versa).
2. **Idempotent Consumer / Inbox Pattern (no Execution Service):**
   - Protegido contra duplicidade de mensagens na rede através de chave de idempotência (`osId`).
   - Se o evento for reentregue pelo broker, a operação não duplicará a `OrdemProducao`.
3. **Dead-Letter Exchange (DLQ):**
   - Caso o Execution Service falhe repetidamente ao processar uma mensagem (ex.: payload corrompido), após 3 tentativas com backoff exponencial ela é encaminhada para `execution-ordem-servico-aberta_error` para auditoria sem travar a fila.
