# Order Processing System

A microservices-based order processing system demonstrating event-driven architecture with .NET 8, RabbitMQ, PostgreSQL, and Redis.

## 🏗️ Architecture

This project implements a distributed order processing system with three microservices:

- **Order Service**: Handles order creation and management
- **Inventory Service**: Manages inventory and validates stock availability
- **Notification Service**: Sends notifications about order status

### Key Technologies

- **.NET 8**: Modern framework for building microservices
- **RabbitMQ**: Message broker for event-driven communication
- **PostgreSQL**: Relational database for data persistence
- **Redis**: In-memory cache for performance optimization
- **Docker & Docker Compose**: Containerization and orchestration
- **Entity Framework Core**: ORM for database operations

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
    │       ├── Messaging/          # Messaging interfaces
    │       │   └── IMessagePublisher.cs
    │       └── Constants/          # Shared constants
    ├── OrderService/               # Order management service (Coming soon)
    ├── InventoryService/           # Inventory management service (Coming soon)
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
- [ ] **Stage 2**: Order Service - Basic API & Database
- [ ] **Stage 3**: RabbitMQ Integration - Publisher
- [ ] **Stage 4**: Inventory Service - Consumer & Database
- [ ] **Stage 5**: Notification Service - Consumer with DLQ
- [ ] **Stage 6**: Redis Integration - Caching
- [ ] **Stage 7**: Dockerization - All Services
- [ ] **Stage 8**: Testing & Documentation

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

---

**Built with ❤️ using .NET 8 and modern microservices patterns**
