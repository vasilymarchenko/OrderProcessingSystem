namespace OrderService.Application.DTOs;

public record PagedOrderResponse(
    IReadOnlyList<OrderResponse> Orders,
    string? NextCursor,
    bool HasMore
);
