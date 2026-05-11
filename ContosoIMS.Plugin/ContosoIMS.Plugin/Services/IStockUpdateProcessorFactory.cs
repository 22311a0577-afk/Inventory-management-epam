using System;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Creates a fully-wired <see cref="IStockUpdateProcessor"/> for the current execution.
    /// Encapsulates composition (poor-man's DI) so the plugin entry-point stays thin.
    /// </summary>
    public interface IStockUpdateProcessorFactory
    {
        IStockUpdateProcessor Create(IServiceProvider serviceProvider, PluginConfig config);
    }
}
