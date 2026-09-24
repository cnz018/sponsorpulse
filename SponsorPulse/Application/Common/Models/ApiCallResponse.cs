using System.Net;

namespace SponsorPulse.Application.Common.Models;

public record ApiCallResponse<T>(
    bool Success,
    HttpStatusCode StatusCode,
    T? Data,
    string? ErrorMessage
)
{
    public static ApiCallResponse<T> Succeeded(HttpStatusCode statusCode, T data) =>
        new(true, statusCode, data, null);

    public static ApiCallResponse<T> Failed(HttpStatusCode statusCode, string errorMessage) =>
        new(false, statusCode, default, errorMessage);
}
