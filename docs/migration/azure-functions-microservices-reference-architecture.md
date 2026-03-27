# Azure Functions Microservices Reference Architecture

This is a proposed decomposition of the current Functions-based system into microservices, with Azure-native supporting services for API governance, async workflows, security, and observability.

```mermaid
flowchart LR
    U["Clients / Admin UI"] --> FD["Azure Front Door + WAF"]
    FD --> APIM["Azure API Management"]

    APIM --> EPSVC["Episode Service (Azure Functions)"]
    APIM --> PODSVC["Podcast Service (Azure Functions)"]
    APIM --> PUBSVC["Publishing Command Service (Azure Functions)"]

    PUBSVC --> SB["Azure Service Bus Topics"]
    SB --> ORCH["Workflow Orchestrator (Durable Functions)"]
    ORCH --> REDDIT["Reddit Connector Service"]
    ORCH --> TWITTER["Twitter/X Connector Service"]
    ORCH --> BLUESKY["Bluesky Connector Service"]
    ORCH --> CONTENT["Content Publisher Service"]

    DISC["Discovery Service (Azure Functions)"] --> SB
    IDX["Indexer Service (Azure Functions)"] --> SEARCH["Azure AI Search"]

    EPSVC --> COS["Azure Cosmos DB"]
    PODSVC --> COS
    DISC --> COS
    IDX --> COS
    ORCH --> COS

    EPSVC --> KV["Azure Key Vault"]
    PODSVC --> KV
    DISC --> KV
    IDX --> KV
    ORCH --> KV

    EPSVC --> APPINS["Application Insights + Log Analytics"]
    PODSVC --> APPINS
    DISC --> APPINS
    IDX --> APPINS
    ORCH --> APPINS
```

## Suggested service boundaries
- Episode API/commands
- Podcast API/commands
- Discovery ingestion
- Indexing/search projection
- Publishing orchestration (post/tweet/bluesky)

## Azure service roles
- **Azure API Management**: auth, throttling, API versioning, policy control
- **Azure Service Bus**: async event-driven communication between services
- **Durable Functions**: resilient fan-out/fan-in workflows and retries
- **Cosmos DB**: service-owned data containers and read/write isolation
- **Azure AI Search**: read/search projection owned by indexing service
- **Key Vault + Managed Identity**: secure secretless runtime access
- **Application Insights**: distributed tracing and operational telemetry