using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace SponsorPulse.Functions.Middleware;

public sealed class ProblemDetailsExceptionMiddleware(
    ILogger<ProblemDetailsExceptionMiddleware> logger
) : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger = logger;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            string traceId = context.InvocationId;
            _logger.LogError(
                exception,
                "Unhandled exception while executing function {FunctionName}. TraceId: {TraceId}",
                context.FunctionDefinition.Name,
                traceId
            );

            HttpRequestData? request = await context.GetHttpRequestDataAsync();
            if (request is null)
            {
                throw;
            }

            HttpResponseData response = request.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/problem+json");

            var problemDetails = new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "Une erreur interne est survenue.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "La requête n'a pas pu être traitée.",
                Instance = request.Url.AbsolutePath,
            };
            problemDetails.Extensions["traceId"] = traceId;

            await response.WriteAsJsonAsync(problemDetails);
            context.GetInvocationResult().Value = response;
        }
    }
}
