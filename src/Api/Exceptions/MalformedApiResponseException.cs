namespace GrillMaster.Api;

/// <summary>
/// The API responded 2xx but the body was not valid JSON.
/// </summary>
/// <param name="innerException">The underlying <see cref="System.Text.Json.JsonException"/>, when there is one.</param>
public sealed class MalformedApiResponseException(Exception? innerException = null)
    : GrillMenuApiException("API error: the response was not valid JSON.", innerException);
