using System;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Orchestrates the end-to-end stock-update flow for one record.</summary>
    public interface IStockUpdateProcessor
    {
        void Process(Entity target, Guid recordId, Guid initiatingUserId);
    }
}
