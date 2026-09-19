namespace GrillMaster.Api;

/// <summary>
/// Base type for failures while talking to the grill menu API. The top-level handler in
/// <c>Program</c> catches this (and its derived types) and prints the exception's own
/// <see cref="Exception.Message"/> instead of a raw stack trace.
/// </summary>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="innerException">The underlying exception, when there is one.</param>
public abstract class GrillApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
