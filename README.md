# Order Processing System

A microservices-based order processing system demonstrating event-driven architecture with .NET 8, RabbitMQ, PostgreSQL, and Redis.

## 🏗️ Architecture

This project implements a distributed order processing system with three microservices:

- **Order Service**: Handles order creation and management
- **Inventory Service**: Manages inventory and validates stock availability
- **Notification Service**: Sends notifications about order status

### Clean Architecture Principles

The OrderService follows **Clean/Onion Architecture** for maintainability and testability:

```
┌─────────────────────────────────────────────────┐
│            API Layer (Controllers)              │ ← Presentation
│  - Request/Response handling                    │
│  - Input validation (FluentValidation)         │
│  - HTTP concerns                                │
└─────────────────────────────────────────────────┘
                     ↓ depends on
┌─────────────────────────────────────────────────┐
│         Application Layer (Services)            │ ← Business Logic
│  - IOrderService, IOrderRepository             │
│  - Business orchestration                       │
│  - Domain object manipulation                   │
└─────────────────────────────────────────────────┘
                     ↓ depends on
┌─────────────────────────────────────────────────┐
│           Domain Layer (Models)                 │ ← Core
│  - Order, OrderItem (clean POCOs)              │
│  - Business rules and domain logic              │
│  - No infrastructure dependencies               │
└─────────────────────────────────────────────────┘
                     ↑ implements
┌─────────────────────────────────────────────────┐
│       Infrastructure Layer (Persistence)        │ ← External
│  - OrderEntity, OrderItemEntity (EF models)    │
│  - OrderRepository implementation               │
│  - Database context & migrations                │
│  - Mapping: Domain ↔ EF Entities               │
└─────────────────────────────────────────────────┘
```

**Key Benefits:**
- ✅ **Testability**: Easy to mock repositories and services
- ✅ **Maintainability**: Clear separation of concerns
- ✅ **Flexibility**: Swap EF for Dapper, RabbitMQ for Kafka, etc.
- ✅ **Domain-Centric**: Business logic independent of infrastructure
- ✅ **SOLID Principles**: Dependency Inversion, Single Responsibility

### Key Technologies

- **.NET 8**: Modern framework for building microservices
- **RabbitMQ**: Message broker for event-driven communication
- **PostgreSQL**: Relational database for data persistence
- **Redis**: In-memory cache for performance optimization
- **Docker & Docker Compose**: Containerization and orchestration
- **Entity Framework Core**: ORM for database operations
- **FluentValidation**: Declarative validation for request models
- **Clean Architecture**: Separation of concerns with onion architecture principles

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- PowerShell (Windows) or Bash (Linux/Mac)

### 1. Clone the Repository

```bash
cd OrderProcessingSystem
```

### 2. Start Infrastructure Services

Start PostgreSQL, RabbitMQ, and Redis using Docker Compose:

```powershell
docker-compose up -d
```

Verify all services are running:

```powershell
docker-compose ps
```

### 3. Access Management UIs

- **RabbitMQ Management**: http://localhost:15672
  - Username: `guest`
  - Password: `guest`

- **PostgreSQL**: `localhost:5432`
  - Username: `postgres`
  - Password: `postgres`

- **Redis**: `localhost:6379`

### 4. Check Service Status

```powershell
# Check RabbitMQ
docker exec orderprocessing-rabbitmq rabbitmq-diagnostics ping

# Check PostgreSQL
docker exec orderprocessing-postgres pg_isready -U postgres

# Check Redis
docker exec orderprocessing-redis redis-cli ping
```

## 📁 Project Structure

```
OrderProcessingSystem/
├── docker-compose.yml              # Infrastructure services configuration
├── OrderProcessingSystem.sln       # .NET solution file
├── implementation-plan.md          # Detailed implementation guide
├── README.md                       # This file
└── src/
    ├── Shared/
    │   └── OrderProcessingSystem.Shared/
    │       ├── Events/             # Event model definitions
    │       │   ├── OrderPlacedEvent.cs
    │       │   ├── InventoryReservedEvent.cs
    │       │   ├── InventoryInsufficientEvent.cs
    │       │   └── OrderItemDto.cs
    │       ├── Messaging/          # Messaging abstractions
    │       │   ├── IMessagePublisher.cs
    │       │   ├── IMessageConsumer.cs
    │       │   ├── RabbitMqPublisher.cs
    │       │   └── RabbitMqConsumer.cs
    │       └── Constants/          # Shared constants
    ├── OrderService/               # Order management service
    │   ├── Application/            # Clean Architecture - Application layer
    │   │   ├── Interfaces/         # Repository and service abstractions
    │   │   │   ├── IOrderRepository.cs
    │   │   │   └── IOrderService.cs
    │   │   ├── Models/             # Core domain models (clean POCOs)
    │   │   │   ├── Order.cs
    │   │   │   ├── OrderItem.cs
    │   │   │   └── OrderStatus.cs
    │   │   ├── Services/           # Business logic
    │   │   │   └── OrderService.cs
    │   │   ├── DTOs/               # API contracts
    │   │   │   ├── CreateOrderRequest.cs
    │   │   │   ├── OrderResponse.cs
    │   │   │   └── PagedOrderResponse.cs
    │   │   └── Validators/         # FluentValidation validators
    │   │       ├── CreateOrderRequestValidator.cs
    │   │       └── GetOrdersQueryValidator.cs
    │   ├── Infrastructure/         # Infrastructure concerns
    │   │   └── Persistence/
    │   │       ├── OrderDbContext.cs
    │   │       ├── Entities/       # EF-specific models
    │   │       │   ├── OrderEntity.cs
    │   │       │   └── OrderItemEntity.cs
    │   │       ├── Configurations/ # EF Fluent API configurations
    │   │       │   ├── OrderEntityConfiguration.cs
    │   │       │   └── OrderItemEntityConfiguration.cs
    │   │       └── Repositories/   # Repository implementations
    │   │           └── OrderRepository.cs
    │   ├── API/                    # Presentation layer
    │   │   ├── Controllers/
    │   │   │   └── OrdersController.cs
    │   │   └── Filters/            # Action filters
    │   │       └── ValidatePageSizeAttribute.cs
    │   ├── Migrations/             # EF Core migrations
    │   └── Program.cs              # Application entry point
    ├── InventoryService/           # Inventory management service
    │   ├── Data/
    │   ├── Models/
    │   ├── Services/
    │   ├── Migrations/
    │   └── Program.cs
    └── NotificationService/        # Notification service (Coming soon)
```

## 🔄 Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                         Order Service                           │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│  │ REST API     │───▶│ PostgreSQL   │    │ Redis Cache  │     │
│  │ (Port 5001)  │    │ (orders_db)  │    │              │     │
│  └──────────────┘    └──────────────┘    └──────────────┘     │
│         │                                                        │
│         │ Publishes: order.placed                               │
│         ▼                                                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │
                 ┌────────────▼────────────┐
                 │      RabbitMQ           │
                 │   Exchange: orders      │
                 │   Type: Topic           │
                 └────────────┬────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
    order.placed    inventory.reserved  inventory.insufficient
              │               │               │
   ┌──────────▼─────────────┐│               │
   │  Inventory Service     ││               │
   │ ┌──────────────┐       ││               │
   │ │ PostgreSQL   │       ││               │
   │ │(inventory_db)│       ││               │
   │ └──────────────┘       ││               │
   │ ┌──────────────┐       ││               │
   │ │ Redis Cache  │       ││               │
   │ └──────────────┘       ││               │
   └────────────────────────┘│               │
                              │               │
              ┌───────────────┴───────────────┘
              │
              ▼
   ┌─────────────────────────┐
   │  Notification Service   │
   │  ┌──────────────────┐   │
   │  │ Dead Letter      │   │
   │  │ Queue (DLQ)      │   │
   │  │ Max Retries: 3   │   │
   │  └──────────────────┘   │
   └─────────────────────────┘
```

## 🛠️ Development Commands

### Build the Solution

```powershell
dotnet build
```

### Run Order Service

```powershell
cd src/OrderService
dotnet run
```

The Order Service will be available at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`
- Swagger UI: `https://localhost:5001/swagger`

### API Endpoints

**Order Service**:

```http
# Create Order
POST /api/orders
Content-Type: application/json
{
  "customerEmail": "customer@example.com",
  "items": [
    {
      "productCode": "PROD-001",
      "quantity": 5
    }
  ]
}

# Get Order by ID
GET /api/orders/{id}

# List Orders (with cursor pagination)
GET /api/orders?pageSize=20&customerEmail=test@example.com&status=Pending

# Health Checks
GET /health/live    # Liveness probe
GET /health/ready   # Readiness probe (checks DB & RabbitMQ)
```

**Validation Examples**:
- Email must be valid format
- Items list cannot be empty
- Quantity must be greater than 0
- Page size must be between 1-100
- Cursor must be valid timestamp_guid format

See `test-validation.http` for comprehensive validation test examples.

### Run Tests (Coming soon)

```powershell
dotnet test
```

### Stop Infrastructure

```powershell
docker-compose down
```

### Stop and Remove All Data

```powershell
docker-compose down -v
```

## 📊 Monitoring

### RabbitMQ

Access the RabbitMQ Management UI to monitor:
- Exchanges and queues
- Message rates
- Consumer connections
- Dead letter queues

URL: http://localhost:15672

### PostgreSQL

Connect using your favorite database client:
- Host: `localhost`
- Port: `5432`
- Username: `postgres`
- Password: `postgres`

### Redis

Monitor Redis using CLI:

```powershell
docker exec -it orderprocessing-redis redis-cli
> KEYS *
> GET order:{orderId}
> TTL order:{orderId}
```

## 🔧 Configuration

### Connection Strings

The services use the following default connection strings:

**PostgreSQL**:
```
Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres
```

**RabbitMQ**:
```
Host=localhost;Port=5672;Username=guest;Password=guest
```

**Redis**:
```
localhost:6379
```

## 📝 Implementation Status

- [x] **Stage 1**: Project Structure & Infrastructure Setup
- [x] **Stage 2**: Order Service - Basic API & Database
- [x] **Stage 3**: RabbitMQ Integration - Publisher
- [x] **Stage 4**: Inventory Service - Consumer & Database
- [x] **Stage 5**: Clean Architecture Refactoring
  - [x] Onion Architecture implementation
  - [x] Repository pattern with abstractions
  - [x] Business logic in service layer
  - [x] Separation of domain models and EF entities
  - [x] FluentValidation integration
  - [x] Thin controllers with presentation concerns only
- [ ] **Stage 6**: Notification Service - Consumer with DLQ
- [ ] **Stage 7**: Redis Integration - Caching
- [ ] **Stage 8**: Dockerization - All Services
- [ ] **Stage 9**: Testing & Documentation

See `implementation-plan.md` for detailed implementation steps.

## 🤝 Contributing

This is a learning/demonstration project. Feel free to fork and experiment!

## 📄 License

This project is available for educational purposes.

## 🔗 Resources

- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Docker Compose](https://docs.docker.com/compose/)
- [.NET Microservices Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

---

**Built with ❤️ using .NET 8, Clean Architecture, and modern microservices patterns**
