using Serilog.Events;

namespace GrillMaster.IntegrationTests.Fixtures;

/// <summary>Reads scalar property values off log events captured by the in-memory sink.</summary>
internal static class LogEventExtensions
{
    public static T GetScalarValue<T>(this LogEvent logEvent, string propertyName) =>
        (T)((ScalarValue)logEvent.Properties[propertyName]).Value!;
}
