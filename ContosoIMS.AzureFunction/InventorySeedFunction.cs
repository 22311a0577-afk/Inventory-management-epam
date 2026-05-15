using ContosoIMS.AzureFunction.Models;
using ContosoIMS.AzureFunction.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ContosoIMS.AzureFunction
{
    /// <summary>
    /// One-time seeding endpoint that creates inventory.xlsx with the initial
    /// 5 SKUs and an empty Transactions sheet.
    ///
    /// Safety:
    ///   - Refuses to overwrite an existing workbook unless ?force=true is passed.
    ///   - Intended to be called once after deployment, then forgotten.
    ///
    /// Call:
    ///   POST https://contosoimsapp-eacdbqbmgucdcfck.southeastasia-01.azurewebsites.net/api/inventory/seed?code=&lt;key&gt;
    ///   POST https://contosoimsapp-eacdbqbmgucdcfck.southeastasia-01.azurewebsites.net/api/inventory/seed?force=true&amp;code=&lt;key&gt;
    /// </summary>
    public class InventorySeedFunction
    {
        private readonly ILogger<InventorySeedFunction> _logger;
        private readonly InventoryRepository _repo;

        public InventorySeedFunction(
            ILogger<InventorySeedFunction> logger,
            InventoryRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }

        [Function("InventorySeed")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
                Route = "inventory/seed")] HttpRequest req)
        {
            using var scope = LoggingScope.BeginInvocation(_logger, "InventorySeed");
            _logger.LogInformation("InventorySeed triggered.");

            try
            {
                bool force = string.Equals(
                    req.Query["force"].ToString(), "true",
                    StringComparison.OrdinalIgnoreCase);

                var repo = _repo;

                // Guard: don't overwrite an existing workbook unless force=true
                var existing = await repo.ReadAsync();
                if (existing.BlobExisted && !force)
                {
                    return new ConflictObjectResult(new
                    {
                        success = false,
                        message =
                            "inventory.xlsx already exists. Re-run with ?force=true to overwrite.",
                        existingItems = existing.Items.Count,
                        existingTransactions = existing.Transactions.Count
                    });
                }

                DateTime now = DateTime.UtcNow;

                // When force=true on an existing blob, preserve the user's current
                // inventory values and transaction history. We only top up missing
                // baseline transactions so manual-adjustment detection has something
                // to compare against going forward.
                if (existing.BlobExisted && force)
                {
                    int addedBaselines = 0;
                    foreach (var item in existing.Items)
                    {
                        bool hasTxn = existing.Transactions.Any(t =>
                            t.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase));
                        if (hasTxn) continue;

                        bool alert = item.CurrentStock < item.ReorderThreshold;
                        existing.Transactions.Add(new InventoryTransaction
                        {
                            TransactionId = $"TXN-{now:yyyyMMdd-HHmmssff}-SEED-{item.Sku}",
                            Sku = item.Sku,
                            TransactionType = "Inbound",
                            Quantity = item.CurrentStock,
                            StockBefore = 0,
                            StockAfter = item.CurrentStock,
                            Source = "Initial Stock",
                            Notes = "Seed baseline (auto-generated, preserved existing stock).",
                            TransactionDate = now,
                            AlertTriggered = alert,
                            SyncedToDataverse = false
                        });
                        addedBaselines++;
                    }

                    await repo.WriteAsync(existing, existing.ETag);

                    _logger.LogInformation(
                        "Preserved {Items} items / {Txns} transactions; added {New} baseline transaction(s).",
                        existing.Items.Count, existing.Transactions.Count - addedBaselines, addedBaselines);

                    return new OkObjectResult(new
                    {
                        success = true,
                        preserved = true,
                        itemCount = existing.Items.Count,
                        transactionCount = existing.Transactions.Count,
                        newBaselineTransactions = addedBaselines,
                        message = $"Preserved existing data; added {addedBaselines} missing baseline transaction(s)."
                    });
                }

                // Fresh seed path (no existing blob).
                var seed = new InventoryWorkbook
                {
                    Items = new List<InventoryItem>
                {
                    new InventoryItem
                    {
                        Sku = "SKU-001",
                        ProductName      = "Dell Monitor 24in",
                        CurrentStock     = 22,
                        ReorderThreshold = 10,
                        StockStatus      = "Active",
                        LastUpdated      = now
                    },
                    new InventoryItem
                    {
                        Sku = "SKU-002",
                        ProductName      = "Hp Mouse",
                        CurrentStock     = 7,
                        ReorderThreshold = 10,
                        StockStatus      = "Critical",
                        LastUpdated      = now
                    },
                    new InventoryItem
                    {
                        Sku = "SKU-003",
                        ProductName      = "A4 Sheets",
                        CurrentStock     = 300,
                        ReorderThreshold = 100,
                        StockStatus      = "Active",
                        LastUpdated      = now
                    },
                    new InventoryItem
                    {
                        Sku = "SKU-004",
                        ProductName      = "Office Chair",
                        CurrentStock     = 1,
                        ReorderThreshold = 11,
                        StockStatus      = "Critical",
                        LastUpdated      = now
                    },
                    new InventoryItem
                    {
                        Sku = "SKU-005",
                        ProductName      = "Wood Planks",
                        CurrentStock     = 20,
                        ReorderThreshold = 80,
                        StockStatus      = "Critical",
                        LastUpdated      = now
                    }
                },
                    Transactions = new List<InventoryTransaction>()
                };

                // Create an initial baseline transaction per SKU so future user
                // edits in Excel have something to compute a delta against.
                // Each baseline is an "Inbound" of the seeded quantity (stock 0 -> N).
                for (int i = 0; i < seed.Items.Count; i++)
                {
                    var item = seed.Items[i];
                    bool alert = item.CurrentStock < item.ReorderThreshold;

                    seed.Transactions.Add(new InventoryTransaction
                    {
                        TransactionId = $"TXN-{now:yyyyMMdd-HHmmssff}-SEED-{item.Sku}",
                        Sku = item.Sku,
                        TransactionType = "Inbound",
                        Quantity = item.CurrentStock,
                        StockBefore = 0,
                        StockAfter = item.CurrentStock,
                        Source = "Initial Stock",
                        Notes = "Seed baseline (auto-generated).",
                        TransactionDate = now,
                        AlertTriggered = alert,
                        SyncedToDataverse = false
                    });
                }

                // Pass the existing ETag (default if new) so a concurrent writer
                // can't be silently clobbered.
                await repo.WriteAsync(seed, force ? existing.ETag : default);

                _logger.LogInformation(
                    "Seeded inventory.xlsx with {Count} items.", seed.Items.Count);

                return new OkObjectResult(new
                {
                    success = true,
                    overwritten = existing.BlobExisted,
                    itemCount = seed.Items.Count,
                    message = "inventory.xlsx created successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InventorySeed failed.");
                return new ObjectResult(new
                {
                    success = false,
                    message = "Internal error during seeding."
                })
                { StatusCode = 500 };
            }
        }
    }
}
