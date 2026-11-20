using Microsoft.AspNetCore.Antiforgery;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return AccountErrors.CsrfFailed();
        }

        return await next(context);
    }
}
