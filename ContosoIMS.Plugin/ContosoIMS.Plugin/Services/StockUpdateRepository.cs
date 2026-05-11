using System;
using ContosoIMS.Plugin.Constants;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class StockUpdateRepository : IStockUpdateRepository
    {
        private const int TransactionIdMaxLength   = 100;
        private const int ResponseMessageMaxLength = 500;

        private readonly IOrganizationService _service;
        private readonly ITracingService _tracing;

        public StockUpdateRepository(IOrganizationService service, ITracingService tracing)
        {
            _service = service;
            _tracing = tracing;
        }

        public void UpdateOutcome(
            Guid recordId,
            int processingStatus,
            int newStockLevel,
            bool alertTriggered,
            string transactionId,
            string responseMessage)
        {
            try
            {
                var update = new Entity(SchemaNames.StockUpdateRequestEntity, recordId);
                update[SchemaNames.Status]          = new OptionSetValue(processingStatus);
                update[SchemaNames.NewStockLevel]   = newStockLevel;
                update[SchemaNames.AlertTriggered]  = alertTriggered;
                update[SchemaNames.TransactionId]   = StringHelpers.Truncate(transactionId, TransactionIdMaxLength);
                update[SchemaNames.ResponseMessage] = StringHelpers.Truncate(responseMessage, ResponseMessageMaxLength);

                _service.Update(update);
                _tracing.Trace("Record updated successfully.");
            }
            catch (Exception ex)
            {
                _tracing.Trace("UpdateRecord failed: " + ex.Message);
                throw;
            }
        }
    }
}
