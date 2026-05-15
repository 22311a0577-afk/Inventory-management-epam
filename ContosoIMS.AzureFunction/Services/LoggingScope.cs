using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ContosoIMS.AzureFunction.Services
{
    /// <summary>
    /// Helpers for attaching a correlation ID to every function invocation so
    /// logs/telemetry can be traced end-to-end in Application Insights.
    /// </summary>
    internal static class LoggingScope
    {
        /// <summary>
        /// Opens a logging scope tagged with a CorrelationId and FunctionName.
        /// Reuses the current <see cref="Activity"/> TraceId when present so
        /// it lines up with Application Insights' operation_Id.
        /// </summary>
        public static IDisposable? BeginInvocation(ILogger logger, string functionName)
        {
            string correlationId =
                Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

            return logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["FunctionName"] = functionName
            });
        }
    }
}
