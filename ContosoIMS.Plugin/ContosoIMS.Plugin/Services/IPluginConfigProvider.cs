using ContosoIMS.Plugin.Models;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Parses the plugin unsecure config string into a <see cref="PluginConfig"/>.</summary>
    public interface IPluginConfigProvider
    {
        PluginConfig GetConfig(string unsecureConfig);
    }
}
