namespace GrillMaster.Api;

/// <summary>
/// The API could not be reached at all (DNS, connection refused, timeout, TLS).
/// </summary>
/// <param name="innerException">The underlying transport exception.</param>
public sealed class ApiUnreachableException(Exception? innerException = null)
    : GrillApiException("API error: could not reach the grill menu API.", innerException);
