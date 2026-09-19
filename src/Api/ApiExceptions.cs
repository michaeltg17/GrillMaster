using System.Net;

namespace GrillMaster.Api;

/// <summary>
/// Base type for failures while talking to the grill menu API. The top-level handler in
/// <c>Program</c> catches this (and its derived types) and prints a clean, user-facing message
/// instead of a raw stack trace.
/// </summary>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="innerException">The underlying exception, when there is one.</param>
public abstract class GrillApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>The API responded with a non-success (non-2xx) status code.</summary>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="statusCode">The HTTP status code the API returned.</param>
public sealed class ApiErrorException(string message, HttpStatusCode statusCode)
    : GrillApiException(message)
{
    /// <summary>The HTTP status code the API returned.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>The API could not be reached at all (DNS, connection refused, timeout, TLS).</summary>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="innerException">The underlying transport exception.</param>
public sealed class ApiUnreachableException(string message, Exception? innerException = null)
    : GrillApiException(message, innerException);

/// <summary>The API responded 2xx but the body was not valid JSON.</summary>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="innerException">The underlying <see cref="System.Text.Json.JsonException"/>.</param>
public sealed class MalformedApiResponseException(string message, Exception? innerException = null)
    : GrillApiException(message, innerException);
