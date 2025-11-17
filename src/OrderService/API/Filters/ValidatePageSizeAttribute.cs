using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrderService.API.Filters;

public class ValidatePageSizeAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("pageSize", out var pageSizeObj) && pageSizeObj is int pageSize)
        {
            if (pageSize < 1 || pageSize > 100)
            {
                context.Result = new BadRequestObjectResult("Page size must be between 1 and 100.");
                return;
            }
        }

        if (context.ActionArguments.TryGetValue("cursor", out var cursorObj) && cursorObj is string cursor && !string.IsNullOrWhiteSpace(cursor))
        {
            var parts = cursor.Split('_');
            if (parts.Length != 2 ||
                !DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out _) ||
                !Guid.TryParse(parts[1], out _))
            {
                context.Result = new BadRequestObjectResult("Invalid cursor format. Expected format: 'timestamp_guid'.");
                return;
            }
        }

        base.OnActionExecuting(context);
    }
}
