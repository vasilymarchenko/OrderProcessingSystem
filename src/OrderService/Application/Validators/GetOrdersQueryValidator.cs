using FluentValidation;

namespace OrderService.Application.Validators;

public class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public GetOrdersQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Cursor)
            .Must(BeValidCursorFormat)
            .When(x => !string.IsNullOrWhiteSpace(x.Cursor))
            .WithMessage("Invalid cursor format. Expected format: 'timestamp_guid'.");

        RuleFor(x => x.CustomerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("Invalid email format.");
    }

    private bool BeValidCursorFormat(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        var parts = cursor.Split('_');
        return parts.Length == 2 &&
               DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out _) &&
               Guid.TryParse(parts[1], out _);
    }
}

public class GetOrdersQuery
{
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
    public string? CustomerEmail { get; set; }
    public int? Status { get; set; }
}
