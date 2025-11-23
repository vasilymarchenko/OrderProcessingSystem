# Outbox Pattern Implementation

## Overview

This document describes the reliable messaging implementation using the **Outbox Pattern** combined with **RabbitMQ Publisher Confirms** and **Mandatory Flag** to ensure no messages are lost in a distributed system.

## Problem Statement

**The Challenge:**
When publishing events from OrderService to RabbitMQ, several failure scenarios can occur:
1. **RabbitMQ broker is down** → Message never reaches broker
2. **Consumer (InventoryService) is down** → No queue exists to receive the message
3. **Network issues** → Message lost in transit
4. **Application crashes** after saving order but before publishing event

**Traditional Approach (Fire-and-Forget):**
```csharp
await _repository.AddAsync(order);
await _publisher.PublishAsync("order.placed", orderEvent); // ❌ If this fails, order exists but no event!
```

**Problem:** Database transaction commits, but message publishing fails → **data inconsistency**.

---

## Solution Architecture

### 1. Outbox Pattern

**Core Principle:** Store events in the database alongside business data in the same transaction.

#### Components

**OutboxMessage Entity:**
```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; }
    public string RoutingKey { get; set; }
    public string Payload { get; set; }
    public OutboxMessageStatus Status { get; set; } // Pending, Published, Failed
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRetryAt { get; set; }
}
```

**OrderService Flow:**
```csharp
// 1. Create order and outbox message
var order = new Order { ... };
var outboxMessage = new OutboxMessage { 
    Payload = JsonSerializer.Serialize(orderPlacedEvent),
    Status = OutboxMessageStatus.Pending,
    ...
};

// 2. Save BOTH in single transaction (atomic operation)
await _repository.AddOrderWithOutboxAsync(order, outboxMessage);

// 3. Return immediately to customer (fast response)
return order;
```

**Background Worker (OutboxPublisherService):**
- Polls database every 5 seconds for pending messages
- Publishes to RabbitMQ
- Updates status based on result
- Implements exponential backoff retry (up to 5 attempts)

---

### 2. Enhanced RabbitMQ Publisher

**Goal:** Detect when messages cannot be routed to any queue.

#### Key Features

**A. Publisher Confirms**
```csharp
await _channel.ConfirmSelectAsync();
await _channel.BasicPublishAsync(...);
await _channel.WaitForConfirmsOrDieAsync(); // ✅ Broker confirms receipt
```
- Ensures broker **received** the message
- Throws exception if broker rejects (disk full, etc.)
- **Does NOT** tell us if message was **routed to a queue**

**B. Mandatory Flag + Return Handler**
```csharp
await _channel.BasicPublishAsync(
    exchange: _exchangeName,
    routingKey: routingKey,
    mandatory: true, // ← Key setting
    ...
);

_channel.BasicReturnAsync += (sender, args) => {
    // Fires when no queue can receive the message
    tcs.SetResult(PublishResult.FailedNoRoute);
};
```
- `mandatory: true` → Broker notifies if message cannot be routed
- Return callback fires **asynchronously** on separate thread
- Must coordinate with publishing thread using `TaskCompletionSource`

**C. Coordination Mechanism**
```csharp
// Thread-safe dictionary tracks pending publishes
ConcurrentDictionary<string, TaskCompletionSource<PublishResult>> _pendingReturns;

// Publishing thread:
var tcs = new TaskCompletionSource<PublishResult>();
_pendingReturns[messageId] = tcs;
await _channel.BasicPublishAsync(...);
await tcs.Task.WaitAsync(timeout: 100ms); // Wait for callback

// Callback thread:
if (_pendingReturns.TryRemove(messageId, out var tcs))
    tcs.SetResult(PublishResult.FailedNoRoute);
```

---

## Implementation Details

### Flow Diagram

```
┌─────────────┐
│ HTTP Request│
│ Create Order│
└──────┬──────┘
       │
       ▼
┌──────────────────────────────────┐
│ OrderService.CreateOrderAsync    │
│                                  │
│ 1. Create Order entity           │
│ 2. Create OutboxMessage          │
│ 3. Save BOTH (single transaction)│
└──────┬───────────────────────────┘
       │
       ▼
┌──────────────┐
│ Return 201   │ ◄─── Fast response to customer
└──────────────┘

       ║ (Async background process)
       ▼
┌────────────────────────────────────┐
│ OutboxPublisherService             │
│ (polls every 5 seconds)            │
│                                    │
│ 1. Query pending messages          │
│ 2. Publish to RabbitMQ             │
│ 3. Check result from publisher     │
│ 4. Update outbox status            │
└──────┬─────────────────────────────┘
       │
       ├─── Success ──────────────────────────────┐
       │                                          │
       └─── FailedNoRoute ────────────────┐       │
                                          │       │
                                          ▼       ▼
                                    ┌──────────────────┐
                                    │ Retry with       │
                                    │ exponential      │
                                    │ backoff          │
                                    └──────────────────┘
```

### RabbitMQ Publisher - Detailed Logic

**Publish Flow:**
```
1. Create TaskCompletionSource (TCS)
2. Register TCS in dictionary with messageId
3. Publish message to RabbitMQ
4. Wait for broker confirm
5. Wait 100ms for return callback
   ├─ If callback fires → Return FailedNoRoute
   └─ If timeout → Return Success
6. Clean up TCS from dictionary
```

**Why 100ms Timeout?**
- RabbitMQ routing is synchronous internally
- Callback typically fires within **single-digit milliseconds**
- 100ms provides generous buffer for:
  - Network jitter
  - GC pauses
  - Thread scheduling delays
- Still fast enough to feel instant (<200ms imperceptible to users)

**Race Condition Handling:**
```csharp
await _channel.WaitForConfirmsOrDieAsync();
// ⚠️ Callback might fire AFTER this returns

await tcs.Task.WaitAsync(timeout: 100ms);
// ✅ Safe: Wait for callback or timeout
```

---

## Pros and Cons

### ✅ Advantages

**1. Reliability**
- **Atomic writes:** Order + Event saved together (no partial failures)
- **No message loss:** Events persisted before publishing
- **Detects routing failures:** Knows when no queue exists
- **Automatic retry:** Exponential backoff up to 5 attempts

**2. Performance**
- **Fast API response:** Customer gets 201 immediately (<50ms)
- **Async publishing:** Doesn't block order creation
- **Optimized timeout:** 100ms vs 1000ms (10x faster than naive approach)

**3. Observability**
- **Audit trail:** All events tracked in database
- **Retry visibility:** Can query failed messages
- **Error logging:** Detailed failure reasons stored

**4. Scalability**
- **Horizontal scaling:** Multiple service instances work independently
- **No shared state:** Each instance manages its own TCS dictionary
- **Batch processing:** Background worker processes 100 messages at once

**5. Testability**
- **Clear separation:** Business logic doesn't know about RabbitMQ
- **Mockable publisher:** IMessagePublisher easy to stub
- **Observable state:** Can query outbox table in tests

### ❌ Disadvantages

**1. Complexity**
- **Additional table:** OutboxMessages schema
- **Background worker:** Extra service to manage
- **Code overhead:** ~300 lines for publisher logic
- **Learning curve:** TaskCompletionSource pattern not trivial

**2. Eventual Consistency**
- **Delay:** Events published seconds after order created (5s polling interval)
- **Not real-time:** Consumers see events asynchronously
- **Order dependency:** Must handle out-of-order delivery

**3. Database Load**
- **Extra writes:** Every order creates 2 rows (Order + Outbox)
- **Polling queries:** SELECT every 5 seconds
- **Cleanup needed:** Old published messages accumulate

**4. Edge Cases**
- **Worker failure:** If worker crashes, messages stuck until restart
- **Poison messages:** Malformed payloads can break retry loop
- **Clock skew:** Distributed timers might behave unexpectedly

**5. Race Condition Window**
- **100ms assumption:** Callback *should* fire within 100ms, but not guaranteed
- **False positives possible:** Extremely slow network could trigger timeout before callback
- **No perfect solution:** Trade-off between speed and reliability

---

## Trade-offs Analysis

### Alternative: Direct Publishing (No Outbox)

**What we gave up:**
```csharp
await _repository.AddAsync(order);
await _publisher.PublishAsync("order.placed", orderEvent);
```

| Aspect | Direct Publishing | Outbox Pattern |
|--------|------------------|----------------|
| **Code Complexity** | Simple ✅ | Complex ❌ |
| **Reliability** | Can lose messages ❌ | Guaranteed delivery ✅ |
| **Performance** | Synchronous (slower) ❌ | Async (faster) ✅ |
| **Database Load** | Lower ✅ | Higher ❌ |
| **Observability** | None ❌ | Full audit trail ✅ |

**When to use Direct Publishing:**
- Non-critical events (analytics, logs)
- High-volume, low-value messages
- Development/testing environments

**When to use Outbox:**
- Critical business events (orders, payments)
- Must guarantee delivery
- Need audit trail
- Production systems

---

### Alternative: Transactional Outbox with CDC

**Change Data Capture (e.g., Debezium):**
- Reads database transaction log
- Publishes changes automatically
- No polling needed

**Comparison:**

| Aspect | Our Approach | CDC Approach |
|--------|-------------|--------------|
| **Simplicity** | ✅ Pure .NET/EF Core | ❌ Requires external tool |
| **Latency** | ~5 seconds (polling) | ~500ms (log streaming) |
| **Infrastructure** | ✅ Just database | ❌ Kafka/Debezium setup |
| **Flexibility** | ✅ Custom retry logic | ❌ Less control |
| **Learning Curve** | ✅ Standard patterns | ❌ New tech stack |

**Recommendation:** Start with polling-based outbox, migrate to CDC if latency becomes critical.

---

### Alternative: Event Sourcing

**Event Sourcing:**
- Store events as source of truth
- Rebuild state from events
- Natural fit for messaging

**Comparison:**

| Aspect | Outbox Pattern | Event Sourcing |
|--------|---------------|----------------|
| **Conceptual Shift** | Small (add table) | Large (rethink data model) |
| **Query Complexity** | ✅ Direct SQL | ❌ Event replay |
| **Audit Trail** | Good | Perfect |
| **Implementation Time** | Days | Weeks/Months |

**Recommendation:** Outbox pattern is pragmatic middle ground for most projects.

---

## Configuration & Tuning

### Key Parameters

**Polling Interval (OutboxPublisherService):**
```csharp
private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
```
- **Lower (1-2s):** More real-time, higher DB load
- **Higher (10-30s):** Lower load, slower event delivery
- **Recommendation:** 5s for balanced approach

**Return Callback Timeout (RabbitMqPublisher):**
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
```
- **Lower (50ms):** Faster, risk false positives
- **Higher (500ms):** Safer, slower
- **Recommendation:** 100ms tested with RabbitMQ on same network

**Max Retry Count:**
```csharp
private readonly int _maxRetryCount = 5;
```
- **Lower (3):** Faster failure detection
- **Higher (10):** More resilient to temporary outages
- **Recommendation:** 5 with exponential backoff = ~1 minute total

**Exponential Backoff:**
```csharp
message.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount));
```
- Retry delays: 2s, 4s, 8s, 16s, 32s
- Total time: ~62 seconds
- Prevents thundering herd on consumer restart

---

## Production Considerations

### Monitoring

**Key Metrics to Track:**
1. **Outbox table size:** `SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Pending'`
   - Alert if > 1000 (publishing stalled)
2. **Failed messages:** `SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Failed'`
   - Alert if > 10 (consumer issues)
3. **Publish latency:** `SELECT AVG(DATEDIFF(second, CreatedAt, PublishedAt))`
   - Alert if > 30s (performance degradation)
4. **Old messages:** `SELECT COUNT(*) WHERE CreatedAt < NOW() - INTERVAL '1 day'`
   - Cleanup job needed

### Cleanup Strategy

**Option 1: Archive Published Messages**
```sql
-- Move to archive table
INSERT INTO OutboxMessagesArchive 
SELECT * FROM OutboxMessages 
WHERE Status = 'Published' AND PublishedAt < NOW() - INTERVAL '7 days';

DELETE FROM OutboxMessages 
WHERE Status = 'Published' AND PublishedAt < NOW() - INTERVAL '7 days';
```

**Option 2: Hard Delete**
```sql
DELETE FROM OutboxMessages 
WHERE Status = 'Published' AND PublishedAt < NOW() - INTERVAL '1 day';
```

**Recommendation:** Run cleanup job daily off-peak hours.

### High Availability

**Multiple Service Instances:**
- Each instance runs OutboxPublisherService
- Workers compete for same pending messages
- Use row-level locking:
  ```csharp
  var messages = await dbContext.OutboxMessages
      .FromSqlRaw("SELECT * FROM OutboxMessages WHERE Status = 0 FOR UPDATE SKIP LOCKED")
      .Take(100)
      .ToListAsync();
  ```

**Consumer Failure Recovery:**
- If InventoryService down, messages marked `FailedNoRoute`
- Retry every 2-32 seconds (exponential)
- When consumer starts, creates queue
- Next retry succeeds automatically

### Security

**Payload Encryption (Optional):**
```csharp
var encrypted = _encryptor.Encrypt(JsonSerializer.Serialize(orderEvent));
var outboxMessage = new OutboxMessage { Payload = encrypted, ... };
```

**Why:**
- Outbox contains sensitive customer data
- Database backups might be less secure than app tier
- Compliance requirements (PCI-DSS, GDPR)

---

## Testing Strategy

### Unit Tests

**RabbitMqPublisher:**
```csharp
[Fact]
public async Task PublishAsync_WhenQueueExists_ReturnsSuccess()
{
    // Arrange: Mock IChannel with no return callback
    // Act: Publish message
    // Assert: Result == PublishResult.Success
}

[Fact]
public async Task PublishAsync_WhenNoQueue_ReturnsFailedNoRoute()
{
    // Arrange: Mock IChannel to fire BasicReturnAsync
    // Act: Publish message
    // Assert: Result == PublishResult.FailedNoRoute
}
```

**OutboxPublisherService:**
```csharp
[Fact]
public async Task ProcessPendingMessages_WhenPublishFails_IncrementsRetryCount()
{
    // Arrange: Create pending outbox message, mock publisher to return failure
    // Act: Process messages
    // Assert: RetryCount++, Status == Failed, NextRetryAt set
}
```

### Integration Tests

**Full Flow:**
```csharp
[Fact]
public async Task CreateOrder_WhenInventoryServiceDown_EventuallyRetries()
{
    // Arrange: Stop InventoryService container
    // Act: Create order via API
    // Assert: 
    //   1. Order saved (201 response)
    //   2. Outbox message created (Pending)
    //   3. Wait for publish attempt
    //   4. Outbox status == Failed (no queue)
    //   5. Start InventoryService
    //   6. Wait for retry
    //   7. Outbox status == Published
    //   8. Inventory reserved
}
```

### Load Testing

**Concurrent Order Creation:**
```csharp
// 100 concurrent requests
await Task.WhenAll(
    Enumerable.Range(0, 100)
        .Select(_ => httpClient.PostAsync("/orders", orderJson))
);

// Verify: No duplicate messages, all orders have outbox entries
```

---

## Migration Path

### Phase 1: Add Outbox (No Breaking Changes)
1. Create `OutboxMessages` table
2. Update repository to save outbox entries
3. Keep direct publishing for now
4. Deploy and monitor

### Phase 2: Enable Background Worker
1. Register `OutboxPublisherService`
2. Run in parallel with direct publishing (dual-write)
3. Monitor for duplicates
4. Verify background worker works

### Phase 3: Remove Direct Publishing
1. Remove `IMessagePublisher` from OrderService
2. Let outbox handle all publishing
3. Monitor latency impact
4. Done!

---

## Conclusion

### Summary

The Outbox Pattern with enhanced RabbitMQ publisher provides:
- ✅ **Guaranteed delivery** via database persistence
- ✅ **Routing failure detection** via mandatory flag + return handler
- ✅ **Fast API responses** via async background processing
- ✅ **Automatic retry** with exponential backoff
- ✅ **Production-ready** monitoring and cleanup strategies

### Recommendation

**Use this approach when:**
- Events are critical business operations
- You need guaranteed delivery
- You can tolerate eventual consistency (seconds delay)
- You want audit trail of all events

**Don't use when:**
- Events are non-critical (analytics, metrics)
- Real-time delivery required (<100ms)
- High-volume (>10k/sec) low-value messages
- Team lacks distributed systems experience

### Next Steps

1. **Test in staging** with InventoryService intentionally down
2. **Monitor metrics** for 1 week in production
3. **Tune parameters** based on actual latency/load
4. **Add cleanup job** when outbox > 10k rows
5. **Consider CDC migration** if 5s latency becomes issue

---

## References

- [Outbox Pattern - Microservices.io](https://microservices.io/patterns/data/transactional-outbox.html)
- [RabbitMQ Publisher Confirms](https://www.rabbitmq.com/confirms.html)
- [RabbitMQ Mandatory Flag](https://www.rabbitmq.com/publishers.html#unroutable)
- [TaskCompletionSource - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1)

---

**Document Version:** 1.0  
**Last Updated:** November 23, 2025  
**Author:** OrderProcessingSystem Team
