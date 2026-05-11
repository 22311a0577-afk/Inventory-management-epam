using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Validates the IPluginExecutionContext and exposes the Target / record id.</summary>
    public interface IPluginContextValidator
    {
        PluginContextResult Validate(IPluginExecutionContext context);
    }
}
