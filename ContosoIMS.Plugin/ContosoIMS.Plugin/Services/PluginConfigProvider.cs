using System;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class PluginConfigProvider : IPluginConfigProvider
    {
        public PluginConfig GetConfig(string unsecureConfig)
        {
            if (string.IsNullOrWhiteSpace(unsecureConfig))
                throw new InvalidPluginExecutionException("Plugin configuration is missing.");

            try
            {
                var config = JsonConvert.DeserializeObject<PluginConfig>(unsecureConfig);
                if (config == null || string.IsNullOrEmpty(config.FunctionUrl))
                    throw new InvalidPluginExecutionException("FunctionUrl missing in config.");

                if (config.FunctionKey == null) config.FunctionKey = string.Empty;
                return config;
            }
            catch (InvalidPluginExecutionException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Invalid plugin config: " + ex.Message);
            }
        }
    }
}
