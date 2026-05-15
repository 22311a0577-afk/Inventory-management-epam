using System;
using ContosoIMS.Plugin.Models;
using ContosoIMS.Plugin.Services;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin
{
    /// <summary>
    /// Dataverse plugin entry-point for the Stock Update flow.
    ///
    /// Responsibilities are delegated to dedicated services that follow SOLID:
    ///   - IPluginConfigProvider       : parses the registration config
    ///   - IPluginContextValidator     : guards pipeline message / stage / target
    ///   - IStockUpdateProcessorFactory: composes a fully-wired processor
    ///   - IStockUpdateProcessor       : orchestrates map / validate / call / persist
    ///
    /// This class is intentionally thin and contains no business logic.
    /// </summary>
    public class StockUpdatePlugin : IPlugin
    {
        private readonly PluginConfig _config;
        private readonly IPluginContextValidator _contextValidator;
        private readonly IStockUpdateProcessorFactory _processorFactory;

        // Dynamics 365 requires the (string unsecure, string secure) constructor.
        public StockUpdatePlugin(string unsecureConfig, string secureConfig)
            : this(unsecureConfig,
                   new PluginConfigProvider(),
                   new PluginContextValidator(),
                   new StockUpdateProcessorFactory())
        {
        }

        // Test-friendly constructor (DIP). Not used by the Dataverse runtime.
        internal StockUpdatePlugin(
            string unsecureConfig,
            IPluginConfigProvider configProvider,
            IPluginContextValidator contextValidator,
            IStockUpdateProcessorFactory processorFactory)
        {
            if (configProvider == null) throw new ArgumentNullException("configProvider");
            if (contextValidator == null) throw new ArgumentNullException("contextValidator");
            if (processorFactory == null) throw new ArgumentNullException("processorFactory");

            _config = configProvider.GetConfig(unsecureConfig);
            _contextValidator = contextValidator;
            _processorFactory = processorFactory;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");

            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context == null)
                throw new InvalidPluginExecutionException("IPluginExecutionContext is not available.");

            PluginContextResult contextResult = _contextValidator.Validate(context);
            if (!contextResult.ShouldExecute)
            {
                if (tracing != null) tracing.Trace("Skipping — " + contextResult.SkipReason);
                return;
            }

            IStockUpdateProcessor processor = _processorFactory.Create(serviceProvider, _config);
            processor.Process(contextResult.Target, contextResult.RecordId, context.InitiatingUserId);
        }
    }
}
