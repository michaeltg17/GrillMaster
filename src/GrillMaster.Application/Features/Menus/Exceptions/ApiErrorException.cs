using System.Net;

namespace GrillMaster.Application.Features.Menus.Exceptions;

/// <summary>
/// The API responded with a non-success (non-2xx) status code. The user-facing message is built from
/// the status code so callers do not have to format it.
/// </summary>
/// <param name="statusCode">The HTTP status code the API returned.</param>
public sealed class ApiErrorException(HttpStatusCode statusCode)
    : GrillMenuApiException($"API error with status code: {(int)statusCode} {statusCode}.")
{
    /// <summary>The HTTP status code the API returned.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}
