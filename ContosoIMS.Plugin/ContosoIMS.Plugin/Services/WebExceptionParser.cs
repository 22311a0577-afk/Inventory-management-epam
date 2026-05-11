using System.IO;
using System.Net;
using ContosoIMS.Plugin.Models;
using Newtonsoft.Json;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Extracts a human-readable error message from a <see cref="WebException"/> raised
    /// while calling the downstream Azure Function (SRP).
    /// </summary>
    public interface IWebExceptionParser
    {
        string Parse(WebException webEx);
    }

    /// <inheritdoc />
    public class WebExceptionParser : IWebExceptionParser
    {
        private readonly IPluginLogger _logger;

        public WebExceptionParser(IPluginLogger logger)
        {
            _logger = logger;
        }

        public string Parse(WebException webEx)
        {
            if (webEx == null) return string.Empty;
            if (webEx.Response == null) return webEx.Message;

            using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
            {
                string raw = reader.ReadToEnd();
                if (_logger != null) _logger.Log("WebException raw response: " + raw);

                try
                {
                    var errResult = JsonConvert.DeserializeObject<AzureFunctionResponse>(raw);
                    return errResult != null && errResult.Message != null ? errResult.Message : raw;
                }
                catch
                {
                    return raw;
                }
            }
        }
    }
}
