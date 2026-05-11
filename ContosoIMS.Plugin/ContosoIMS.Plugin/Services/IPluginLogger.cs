namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Thin abstraction over <see cref="Microsoft.Xrm.Sdk.ITracingService"/> so that
    /// orchestration code can emit structured log entries without owning string-formatting concerns.
    /// </summary>
    public interface IPluginLogger
    {
        void Log(string message);
        void LogFormat(string format, params object[] args);
    }
}
