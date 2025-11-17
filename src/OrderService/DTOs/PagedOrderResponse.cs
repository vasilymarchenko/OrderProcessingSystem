using OrderService.Models;

namespace OrderService.DTOs;

public record PagedOrderResponse(
    IReadOnlyList<OrderResponse> Orders,
    string? NextCursor,
    bool HasMore
);
