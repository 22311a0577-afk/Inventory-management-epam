using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class PluginLogger : IPluginLogger
    {
        private readonly ITracingService _tracing;

        public PluginLogger(ITracingService tracing)
        {
            _tracing = tracing;
        }

        public void Log(string message)
        {
            if (_tracing == null) return;
            _tracing.Trace(message);
        }

        public void LogFormat(string format, params object[] args)
        {
            if (_tracing == null) return;
            _tracing.Trace(format, args);
        }
    }
}
