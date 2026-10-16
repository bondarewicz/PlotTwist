namespace PlotTwist.Api;

using Domain;
using Microsoft.AspNetCore.Diagnostics;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException)
            return false;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails =
                {
                    Title = "The request breaks a domain rule.",
                    Detail = exception.Message,
                    Status = StatusCodes.Status400BadRequest,
                },
            }
        );
    }
}
