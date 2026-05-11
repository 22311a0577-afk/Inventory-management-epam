using System;
using System.IO;
using System.Net;
using System.Text;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class AzureFunctionClient : IAzureFunctionClient
    {
        private const int TimeoutMs = 25000;

        private readonly PluginConfig _config;
        private readonly ITracingService _tracing;

        public AzureFunctionClient(PluginConfig config, ITracingService tracing)
        {
            _config = config;
            _tracing = tracing;
        }

        public AzureFunctionResponse Send(StockUpdateRequest request)
        {
            string jsonPayload = JsonConvert.SerializeObject(request);
            _tracing.Trace("Payload: " + jsonPayload);

            string url = BuildRequestUrl();
            _tracing.Trace("Calling Azure Function: " + _config.FunctionUrl);

            HttpWebRequest http = CreateHttpRequest(url, jsonPayload);

            using (var response = (HttpWebResponse)http.GetResponse())
            {
                return ReadResponse(response);
            }
        }

        private string BuildRequestUrl()
        {
            return string.IsNullOrEmpty(_config.FunctionKey)
                ? _config.FunctionUrl
                : _config.FunctionUrl + "?code=" + _config.FunctionKey;
        }

        private HttpWebRequest CreateHttpRequest(string url, string jsonPayload)
        {
            var http = (HttpWebRequest)WebRequest.Create(url);
            http.Method = "POST";
            http.ContentType = "application/json";
            http.Timeout = TimeoutMs;

            byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
            http.ContentLength = data.Length;

            using (Stream stream = http.GetRequestStream())
                stream.Write(data, 0, data.Length);

            return http;
        }

        private AzureFunctionResponse ReadResponse(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string body = reader.ReadToEnd();
                _tracing.Trace("Azure Function response: " + body);

                var result = JsonConvert.DeserializeObject<AzureFunctionResponse>(body);
                if (result == null)
                    throw new InvalidPluginExecutionException("Empty or unreadable response from Azure Function.");

                return result;
            }
        }
    }
}
