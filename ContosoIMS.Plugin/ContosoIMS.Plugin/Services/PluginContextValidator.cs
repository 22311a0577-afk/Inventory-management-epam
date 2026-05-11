using System;
using ContosoIMS.Plugin.Constants;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class PluginContextValidator : IPluginContextValidator
    {
        // PostOperation stage value
        private const int PostOperationStage = 40;

        public PluginContextResult Validate(IPluginExecutionContext context)
        {
            if (context == null)
                return PluginContextResult.Skip("Context is null.");

            if (context.MessageName != SchemaNames.MessageCreate)
                return PluginContextResult.Skip("not a Create message.");

            if (context.Stage != PostOperationStage)
                return PluginContextResult.Skip("not PostOperation stage.");

            if (!context.InputParameters.Contains(SchemaNames.TargetKey))
                return PluginContextResult.Skip("no Target in InputParameters.");

            var target = context.InputParameters[SchemaNames.TargetKey] as Entity;
            if (target == null)
                return PluginContextResult.Skip("Target is null.");

            if (!context.OutputParameters.Contains(SchemaNames.IdOutputKey))
                return PluginContextResult.Skip("no id in OutputParameters.");

            var recordId = (Guid)context.OutputParameters[SchemaNames.IdOutputKey];
            return PluginContextResult.Run(target, recordId);
        }
    }
}
