using System;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Persists the stock-update outcome back to the originating Dataverse record.</summary>
    public interface IStockUpdateRepository
    {
        void UpdateOutcome(
            Guid recordId,
            int processingStatus,
            int newStockLevel,
            bool alertTriggered,
            string transactionId,
            string responseMessage);
    }
}
