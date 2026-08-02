# Architectural Decision Record & Decomposition Document: Shortly Microservices

## 1. Service Decomposition Rationale

The original **Shortly** monolith bundled all concerns (User Authentication, Razor Pages UI, Short Link Creation, Redirect Engine, and Analytics) within a single ASP.NET Core process backed by a single SQLite database. 

To achieve high horizontal scalability, resilience, and operational independence, the monolith has been decomposed into five domain-driven microservices based on domain boundary analysis:

### 1.1 API Gateway (YARP / ASP.NET Core)
* **Responsibility:** Serves as the single unified entry point for clients.
* **Rationale:** Decouples client applications from internal microservice topologies. Handles cross-cutting concerns like rate-limiting, SSL termination, request routing, and JWT authentication token verification.

### 1.2 Identity Service
* **Responsibility:** User management, registration, login, and JWT token issuing.
* **Rationale:** Authentication operations require secure credential hashing (BCrypt) and isolated persistence. Separating identity ensures security policies and auth databases are isolated from user link data.

### 1.3 URL Management Service
* **Responsibility:** Management of short URLs (creation via ULID tokens, deletion, listing, and updates).
* **Rationale:** URL creation is write-heavy and requires strict business validation. Isolating this domain allows developers to evolve link features without risk to the ultra-high-throughput redirect engine.

### 1.4 Redirect Service
* **Responsibility:** Fast execution of `GET /{shortUrl}` HTTP redirects (`301`/`302`/`307`).
* **Rationale:** Redirection accounts for ~95% of total platform traffic. It requires sub-5ms response times. By keeping this service stateless, lightweight, and backed by a Redis Cache, it can scale independently to handle millions of requests per second.

### 1.5 Stats Service
* **Responsibility:** Processing click events, computing aggregates, and serving analytics dashboards.
* **Rationale:** Analytics writes are resource-intensive. Computing metrics directly during redirect requests degrades latency. Moving click processing to an asynchronous worker service isolates analytical loads from user-facing traffic.


## 2. Communication Patterns & Protocols

The architecture combines **Synchronous HTTP/gRPC** for client interactions with **Asynchronous Event-Driven Messaging** for inter-service workflows.

| Communication Link | Pattern | Protocol | Justification |
| :--- | :--- | :--- | :--- |
| **Client → API Gateway** | Synchronous | HTTP/2, HTTPS / JSON | Standard RESTful API communication for external clients. |
| **Gateway → Internal Services** | Synchronous | gRPC / HTTP REST | gRPC for high-performance intra-cluster calls; REST for standard APIs. |
| **Redirect Service → Redis** | Synchronous | RESP | Sub-millisecond key-value lookups for active short links. |
| **Redirect Service → Broker** | Asynchronous | AMQP (RabbitMQ) | Fire-and-forget event publishing (`LinkVisitedEvent`) ensuring non-blocking redirects. |
| **Broker → Stats Service** | Asynchronous | AMQP Consumer | Batch consumption of click events for analytics persistence. |



## 3. Data Ownership & Consistency Model

Following the **Database-per-Service** pattern, no microservice can access another microservice's database directly.

+------------------+    +-------------------+    +-----------------+
| Identity Service |    |   URL Service     |    |  Stats Service  |
+--------+---------+    +---------+---------+    +--------+--------+
|                        |                       |
[Identity DB]              [URL DB]                [Stats DB]
(PostgreSQL)             (PostgreSQL)             (ClickHouse)

### Data Consistency Strategy
1. **Eventual Consistency:** When a new URL is created in `URL Service`, a `LinkCreatedEvent` is published to RabbitMQ. `Redirect Service` and `Redis Cache` consume this event to pre-warm the cache.
2. **Cache Invalidation:** When a link is deleted or updated, `URL Service` publishes a `LinkInvalidatedEvent` to purge the corresponding key in Redis immediately.
3. **Idempotent Click Processing:** `Stats Service` processes click messages idempotently using event correlation IDs (`X-Request-Id` / Message ID) to prevent duplicate click counts during message redelivery.


## 4. Scalability Considerations

Traffic in a URL shortener is heavily read-skewed (Read-to-Write ratio > 20:1).

* **Redirect Service (Stateless):** Scaled out horizontally using Kubernetes HPA (Horizontal Pod Autoscaler) based on CPU/Request throughput metrics. Supported by a distributed Redis Cluster.
* **URL Service:** Scaled moderately based on link creation demand. Database writes use PostgreSQL connection pooling (PgBouncer).
* **Stats Service:** Scaled based on RabbitMQ queue depth. When queue size spikes, extra worker replicas spin up to drain click events without impacting end users.


## 5. Failure Modes & Resiliency Strategies

| Failure Scenario | Mitigation & Resilience Strategy |
| :--- | :--- |
| **Redis Cache Outage** | Redirect Service falls back to querying the PostgreSQL **URL DB Read Replica**. Circuit Breaker (Polly) limits DB query rate to avoid overwhelming the database. |
| **RabbitMQ Unavailability** | Redirect Service logs click events locally to an in-memory ring buffer / disk-backed fallback logger, continuing to perform 301/302 redirects uninterrupted. |
| **Stats Service Down** | Messages accumulate safely in RabbitMQ durability queues. Once Stats Service recovers, it drains and processes backlogged events without data loss. |
| **Identity Service Down** | Existing valid JWT tokens continue working at the API Gateway level until expiration. Only new logins/registrations are temporarily blocked. |

---

## 6. Proposed Technology Stack

* **API Gateway:** YARP (Yet Another Reverse Proxy) / ASP.NET Core 10
* **Microservices Framework:** ASP.NET Core Minimal APIs (.NET 10)
* **Database (Relational):** PostgreSQL 16 with EF Core / Dapper
* **Database (Analytics):** ClickHouse / PostgreSQL
* **Caching Layer:** Redis 7 (Cluster Mode)
* **Message Broker:** RabbitMQ / Apache Kafka
* **Resilience Framework:** Polly (.NET Resilience Pipelines)
* **Observability:** OpenTelemetry + Prometheus + Grafana + Serilog