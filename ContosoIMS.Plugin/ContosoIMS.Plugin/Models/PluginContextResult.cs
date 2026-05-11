using System;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Models
{
    /// <summary>
    /// Result of inspecting the plugin execution context. Tells the pipeline
    /// whether the plugin should run, and exposes the target entity / record id.
    /// </summary>
    public class PluginContextResult
    {
        public bool ShouldExecute { get; private set; }
        public string SkipReason { get; private set; }
        public Entity Target { get; private set; }
        public Guid RecordId { get; private set; }

        private PluginContextResult(bool shouldExecute, string skipReason, Entity target, Guid recordId)
        {
            ShouldExecute = shouldExecute;
            SkipReason = skipReason;
            Target = target;
            RecordId = recordId;
        }

        public static PluginContextResult Skip(string reason)
        {
            return new PluginContextResult(false, reason, null, Guid.Empty);
        }

        public static PluginContextResult Run(Entity target, Guid recordId)
        {
            return new PluginContextResult(true, null, target, recordId);
        }
    }
}
