using System;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class StockUpdateProcessorFactory : IStockUpdateProcessorFactory
    {
        public IStockUpdateProcessor Create(IServiceProvider serviceProvider, PluginConfig config)
        {
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService orgService = factory.CreateOrganizationService(context.InitiatingUserId);

            IPluginLogger logger = new PluginLogger(tracing);

            return new StockUpdateProcessor(
                new StockUpdateRequestMapper(),
                new StockUpdateValidator(),
                new AzureFunctionClient(config, tracing),
                new StockUpdateRepository(orgService, tracing),
                new WebExceptionParser(logger),
                new UserEmailResolver(orgService),
                logger);
        }
    }
}