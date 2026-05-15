using System.Text.Json;
using Azure;
using ContosoIMS.AzureFunction.Models;
using ContosoIMS.AzureFunction.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ContosoIMS.AzureFunction
{
    public class StockUpdateFunction
    {
        private readonly ILogger<StockUpdateFunction> _logger;
        private readonly InventoryRepository _repo;

        // Retry budget for optimistic-concurrency conflicts (412).
        private const int MaxConcurrencyRetries = 3;

        public StockUpdateFunction(
            ILogger<StockUpdateFunction> logger,
            InventoryRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }

        [Function("StockUpdate")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
                Route = "stock/update")] HttpRequest req)
        {
            using var scope = LoggingScope.BeginInvocation(_logger, "StockUpdate");
            _logger.LogInformation("StockUpdate triggered.");

            // ─── Parse Request (sent by Plugin) ──────────────────────────
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request: {Body}", body);

            StockUpdateRequest? input;
            try
            {
                input = JsonSerializer.Deserialize<StockUpdateRequest>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError("Deserialize failed: {Message}", ex.Message);
                return new BadRequestObjectResult(
                    new StockUpdateResponse { Success = false, Message = "Invalid request body." });
            }

            if (input == null ||
                string.IsNullOrWhiteSpace(input.Sku) ||
                input.Quantity <= 0)
                return new BadRequestObjectResult(
                    new StockUpdateResponse { Success = false, Message = "SKU and Quantity are required." });

            if (input.TransactionType != "Inbound" && input.TransactionType != "Outbound")
                return new BadRequestObjectResult(
                    new StockUpdateResponse
                    {
                        Success = false,
                        Message = "TransactionType must be Inbound or Outbound."
                    });

            try
            {
                var repo = _repo;

                // ─── Read-modify-write with optimistic concurrency ─────────
                for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
                {
                    var workbook = await repo.ReadAsync();

                    var item = workbook.Items.FirstOrDefault(p =>
                        p.Sku.Equals(input.Sku, StringComparison.OrdinalIgnoreCase));

                    if (item == null)
                        return new NotFoundObjectResult(
                            new StockUpdateResponse
                            {
                                Success = false,
                                Message = $"Product '{input.Sku}' not found in inventory."
                            });

                    if (input.TransactionType == "Outbound" &&
                        input.Quantity > item.CurrentStock)
                        return new BadRequestObjectResult(
                            new StockUpdateResponse
                            {
                                Success = false,
                                Message = $"Insufficient stock. " +
                                          $"Current: {item.CurrentStock}, " +
                                          $"Requested: {input.Quantity}"
                            });

                    int previousStock = item.CurrentStock;

                    item.CurrentStock = input.TransactionType == "Inbound"
                        ? item.CurrentStock + input.Quantity
                        : item.CurrentStock - input.Quantity;

                    bool alertTriggered = item.CurrentStock < item.ReorderThreshold;

                    item.StockStatus = item.CurrentStock == 0 ? "OutOfStock"
                                     : alertTriggered ? "Critical"
                                     : "Active";

                    item.LastUpdated = DateTime.UtcNow;

                    if (input.TransactionType == "Inbound")
                        item.LastRestockedDate = DateTime.UtcNow;

                    string transactionId = $"TXN-{DateTime.UtcNow:yyyyMMdd-HHmmssff}";

                    workbook.Transactions.Add(new InventoryTransaction
                    {
                        TransactionId = transactionId,
                        Sku = item.Sku,
                        TransactionType = input.TransactionType,
                        Quantity = input.Quantity,
                        StockBefore = previousStock,
                        StockAfter = item.CurrentStock,
                        RequestedBy = input.RequestedBy ?? "",
                        Source = input.Source ?? "",
                        Notes = input.Notes ?? "",
                        TransactionDate = DateTime.UtcNow,
                        AlertTriggered = alertTriggered,
                        SyncedToDataverse = false
                    });

                    try
                    {
                        await repo.WriteAsync(workbook, workbook.ETag);

                        _logger.LogInformation(
                            "Blob updated. SKU={Sku} Prev={Prev} New={New} Alert={Alert}",
                            input.Sku, previousStock, item.CurrentStock, alertTriggered);

                        return new OkObjectResult(new StockUpdateResponse
                        {
                            Success = true,
                            NewStockLevel = item.CurrentStock,
                            AlertTriggered = alertTriggered,
                            TransactionId = transactionId,
                            Message = $"Stock {input.TransactionType.ToLower()} " +
                                             $"processed. New level: {item.CurrentStock}"
                        });
                    }
                    catch (RequestFailedException ex) when (ex.Status == 412)
                    {
                        // Blob was modified between our read and write — retry.
                        _logger.LogWarning(
                            "Concurrency conflict on attempt {Attempt}/{Max}. Retrying...",
                            attempt, MaxConcurrencyRetries);

                        if (attempt == MaxConcurrencyRetries) throw;
                        await Task.Delay(150 * attempt); // simple backoff
                    }
                }

                // Should never reach here, but keeps compiler happy.
                return new ObjectResult(new StockUpdateResponse
                {
                    Success = false,
                    Message = "Could not complete stock update due to repeated concurrency conflicts."
                })
                { StatusCode = 409 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in StockUpdate.");
                return new ObjectResult(
                    new StockUpdateResponse
                    {
                        Success = false,
                        Message = "Internal error processing stock update."
                    })
                { StatusCode = 500 };
            }
        }
    }
}