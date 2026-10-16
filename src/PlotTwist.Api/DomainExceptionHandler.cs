namespace PlotTwist.Api;

using Microsoft.AspNetCore.Diagnostics;
using PlotTwist.Domain;

// A broken domain rule is the caller's mistake: 400 with the rule's message. Anything else falls through
// to the default handler and stays a 500 without internal details.
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException)
            return false;

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Title = "The request breaks a domain rule.", Detail = exception.Message, Status = StatusCodes.Status400BadRequest },
        });
    }
}
