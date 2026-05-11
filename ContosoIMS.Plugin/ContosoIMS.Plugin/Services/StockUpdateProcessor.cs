using System;
using System.Net;
using ContosoIMS.Plugin.Constants;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>
    /// Coordinates mapping, validation, remote call and persistence.
    /// Pure orchestration — delegates every concrete operation to a collaborator (DIP).
    /// </summary>
    public class StockUpdateProcessor : IStockUpdateProcessor
    {
        private readonly IStockUpdateRequestMapper _mapper;
        private readonly IStockUpdateValidator     _validator;
        private readonly IAzureFunctionClient      _functionClient;
        private readonly IStockUpdateRepository    _repository;
        private readonly IWebExceptionParser       _webExceptionParser;
        private readonly IPluginLogger             _logger;

        public StockUpdateProcessor(
            IStockUpdateRequestMapper mapper,
            IStockUpdateValidator validator,
            IAzureFunctionClient functionClient,
            IStockUpdateRepository repository,
            IWebExceptionParser webExceptionParser,
            IPluginLogger logger)
        {
            _mapper             = mapper;
            _validator          = validator;
            _functionClient     = functionClient;
            _repository         = repository;
            _webExceptionParser = webExceptionParser;
            _logger             = logger;
        }

        public void Process(Entity target, Guid recordId)
        {
            _logger.LogFormat("StockUpdatePlugin started. Record ID: {0}", recordId);

            StockUpdateRequest request = _mapper.Map(target);
            _logger.LogFormat("Read values — SKU: {0}, Type: {1}, Qty: {2}, Source: {3}",
                request.sku, request.transactionType, request.quantity, request.source);

            ValidationResult validation = _validator.Validate(request);
            if (!validation.IsValid)
            {
                _logger.Log("Validation failed — " + validation.ErrorMessage);
                RecordFailure(recordId, validation.ErrorMessage);
                return;
            }

            try
            {
                AzureFunctionResponse result = _functionClient.Send(request);
                PersistResult(recordId, result);
            }
            catch (WebException webEx)
            {
                string error = _webExceptionParser.Parse(webEx);
                _logger.Log("WebException: " + error);
                RecordFailure(recordId, "Function error: " + error);
            }
            catch (Exception ex)
            {
                _logger.Log("Exception: " + ex.Message);
                RecordFailure(recordId, "Plugin error: " + ex.Message);
            }
        }

        private void PersistResult(Guid recordId, AzureFunctionResponse result)
        {
            _repository.UpdateOutcome(
                recordId,
                result.Success ? OptionSetValues.Status_Success : OptionSetValues.Status_Failed,
                result.Success ? result.NewStockLevel : 0,
                result.AlertTriggered,
                result.TransactionId,
                result.Message);

            _logger.LogFormat("Plugin completed — Success: {0}, NewStock: {1}",
                result.Success, result.NewStockLevel);
        }

        private void RecordFailure(Guid recordId, string message)
        {
            _repository.UpdateOutcome(recordId, OptionSetValues.Status_Failed, 0, false, string.Empty, message);
        }
    }
}
