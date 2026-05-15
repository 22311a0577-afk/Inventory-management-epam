using Azure;
using ContosoIMS.AzureFunction.Models;
using ContosoIMS.AzureFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace ContosoIMS.AzureFunction
{
    public class DataverseSyncFunction
    {
        private readonly ILogger<DataverseSyncFunction> _logger;
        private readonly InventoryRepository _repo;
        private readonly IDataverseClientFactory _dataverseClientFactory;

        private const int MaxConcurrencyRetries = 3;

        public DataverseSyncFunction(
            ILogger<DataverseSyncFunction> logger,
            InventoryRepository repo,
            IDataverseClientFactory dataverseClientFactory)
        {
            _logger = logger;
            _repo = repo;
            _dataverseClientFactory = dataverseClientFactory;
        }

        [Function("DataverseSync")]
        public async Task Run(
            [BlobTrigger("inventory/inventory.xlsx",
                Connection = "AzureWebJobsStorage")] byte[] _)
        {
            using var scope = LoggingScope.BeginInvocation(_logger, "DataverseSync");
            _logger.LogInformation("DataverseSync triggered.");

            // Top-level guard so a failure anywhere below is logged but does not
            // bubble up as an unhandled exception (which would re-queue the blob
            // trigger and eventually poison-queue it).
            InventoryWorkbook? workbook = null;
            var repo = _repo;

            try
            {
                // ─── 1. Load workbook (with ETag for concurrency) ───────────
                try
                {
                    workbook = await repo.ReadAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read inventory workbook from blob.");
                    return;
                }

                if (!workbook.BlobExisted || workbook.Items.Count == 0)
                {
                    _logger.LogWarning("Empty or missing workbook — nothing to sync.");
                    return;
                }

                // ─── 2. Detect user-made manual adjustments ─────────────────
                int manualAdjCount = 0;
                try
                {
                    manualAdjCount = DetectManualAdjustments(workbook);
                    if (manualAdjCount > 0)
                        _logger.LogInformation(
                            "Detected {Count} manual adjustment(s) from Excel edits.", manualAdjCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Manual-adjustment detection failed; continuing.");
                }

                // ─── 3. Determine if there's anything to sync ───────────────
                var unsynced = workbook.Transactions.Where(t => !t.SyncedToDataverse).ToList();

                // ─── 4. Sync to Dataverse (best-effort) ─────────────────────
                // Dataverse connection is isolated: if it fails, we still write
                // back any new manual-adjustment rows so the audit trail in the
                // workbook is preserved. They'll be picked up next run when auth
                // is fixed (SyncedToDataverse stays false).
                bool dataverseConnected = false;
                ServiceClient? svc = null;
                try
                {
                    if (unsynced.Count == 0)
                    {
                        _logger.LogInformation("No unsynced transactions — skipping Dataverse step.");
                    }
                    else
                    {
                        svc = _dataverseClientFactory.CreateClient();
                        if (svc != null)
                        {
                            dataverseConnected = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to connect to Dataverse. Workbook changes (if any) will still be saved.");
                }

                if (dataverseConnected && svc != null)
                {
                    // Sync product stock (one update per SKU using latest value from Excel)
                    foreach (var item in workbook.Items)
                    {
                        try { await SyncProductAsync(svc, item); }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Product sync failed for SKU={Sku}", item.Sku);
                        }
                    }

                    // Sync each unsynced transaction; mark as synced on success.
                    foreach (var txn in unsynced)
                    {
                        try
                        {
                            await SyncTransactionAsync(svc, txn);
                            txn.SyncedToDataverse = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Txn sync failed Id={Id} SKU={Sku}", txn.TransactionId, txn.Sku);
                        }
                    }
                }

                svc?.Dispose();

                // ─── 5. Write back the workbook (only if anything changed) ──
                bool anyChange = manualAdjCount > 0
                                 || workbook.Transactions.Any(t => t.SyncedToDataverse
                                                                  && unsynced.Contains(t));
                if (!anyChange)
                {
                    _logger.LogInformation("No workbook changes to persist.");
                    return;
                }

                try
                {
                    _logger.LogInformation(
                        "Writing back workbook: {Items} items, {Txns} transactions ({Unsynced} unsynced).",
                        workbook.Items.Count, workbook.Transactions.Count,
                        workbook.Transactions.Count(t => !t.SyncedToDataverse));
                    await WriteBackWithRetryAsync(repo, workbook);
                    _logger.LogInformation("Workbook write-back completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Workbook write-back failed.");
                }
            }
            catch (Exception ex)
            {
                // Catch-all so the BlobTrigger never gets poison-queued from
                // an unexpected error path.
                _logger.LogError(ex, "Unhandled error in DataverseSync.");
            }

            _logger.LogInformation("DataverseSync completed.");
        }

        // ─── Manual adjustment detection ────────────────────────────────────
        /// <summary>
        /// Compares each inventory item's CurrentStock with the StockAfter of
        /// its most-recent transaction. A mismatch means the user edited the
        /// inventory sheet directly in Excel — we record a synthetic
        /// "Manual Adjustment" transaction to keep the audit trail complete.
        /// Returns the number of manual adjustments created.
        /// </summary>
        private int DetectManualAdjustments(InventoryWorkbook workbook)
        {
            int created = 0;
            DateTime now = DateTime.UtcNow;

            _logger.LogInformation(
                "DetectManualAdjustments: scanning {Items} items against {Txns} transactions.",
                workbook.Items.Count, workbook.Transactions.Count);

            foreach (var item in workbook.Items)
            {
                var latest = workbook.Transactions
                    .Where(t => t.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(t => t.TransactionDate)
                    .FirstOrDefault();

                _logger.LogInformation(
                    "  SKU={Sku} CurrentStock={Cur} LatestTxn={LatestId} LatestStockAfter={LatestAfter}",
                    item.Sku, item.CurrentStock,
                    latest?.TransactionId ?? "(none)",
                    latest?.StockAfter.ToString() ?? "(n/a)");

                // Determine the baseline:
                //   - If a prior transaction exists, baseline = its StockAfter
                //   - If none exists, this SKU has no audit history yet
                //     (e.g., user added a new row in Excel, or seed pre-dated
                //     baseline logic). Treat baseline = 0 and log an
                //     "Initial Stock" Inbound for the full current quantity.
                int baseline;
                bool isInitialBaseline;

                if (latest == null)
                {
                    baseline = 0;
                    isInitialBaseline = true;
                }
                else
                {
                    baseline = latest.StockAfter;
                    isInitialBaseline = false;
                }

                if (baseline == item.CurrentStock) continue; // no edit

                int delta = item.CurrentStock - baseline;
                bool alertTriggered = item.CurrentStock < item.ReorderThreshold;

                item.StockStatus = item.CurrentStock == 0 ? "OutOfStock"
                                 : alertTriggered ? "Critical"
                                 : "Active";
                item.LastUpdated = now;

                string suffix = isInitialBaseline ? "-INIT" : "-MA";
                string source = isInitialBaseline ? "Initial Stock" : "Manual Adjustment";
                string notes = isInitialBaseline
                    ? "Auto-generated initial stock baseline."
                    : "Auto-generated from user edit of Excel inventory sheet.";

                workbook.Transactions.Add(new InventoryTransaction
                {
                    TransactionId = $"TXN-{now:yyyyMMdd-HHmmssff}{suffix}-{item.Sku}",
                    Sku = item.Sku,
                    TransactionType = delta > 0 ? "Inbound" : "Outbound",
                    Quantity = Math.Abs(delta),
                    StockBefore = baseline,
                    StockAfter = item.CurrentStock,
                    Source = source,
                    Notes = notes,
                    TransactionDate = now,
                    AlertTriggered = alertTriggered,
                    SyncedToDataverse = false
                });

                created++;
                _logger.LogInformation(
                    "{Kind}: SKU={Sku} {Before} -> {After} (delta {Delta})",
                    isInitialBaseline ? "Initial baseline" : "Manual adjustment",
                    item.Sku, baseline, item.CurrentStock, delta);
            }

            return created;
        }

        // ─── Sync product row to Dataverse ──────────────────────────────────
        private async Task SyncProductAsync(ServiceClient svc, InventoryItem item)
        {
            var query = new QueryExpression("product")
            {
                ColumnSet = new ColumnSet("productid")
            };
            query.Criteria.AddCondition(
                "productnumber", ConditionOperator.Equal, item.Sku);

            var results = await svc.RetrieveMultipleAsync(query);
            if (results.Entities.Count == 0)
            {
                _logger.LogWarning("SKU={Sku} not found in Dataverse.", item.Sku);
                return;
            }

            Guid productId = results.Entities[0].Id;
            int stockStatus = item.StockStatus switch
            {
                "OutOfStock" => 767270002,
                "Critical" => 767270001,
                _ => 767270000   // Active
            };

            var update = new Entity("product", productId);
            update["cim_currentstock"] = item.CurrentStock;
            update["cim_stockstatus"] = new OptionSetValue(stockStatus);
            update["cim_lowstock"] = item.CurrentStock < item.ReorderThreshold;

            if (item.LastRestockedDate.HasValue)
                update["cim_lastrestockeddate"] = item.LastRestockedDate.Value;

            await svc.UpdateAsync(update);

            _logger.LogInformation(
                "Synced product SKU={Sku} Stock={Stock}", item.Sku, item.CurrentStock);
        }

        // ─── Sync single transaction ────────────────────────────────────────
        private async Task SyncTransactionAsync(ServiceClient svc, InventoryTransaction txn)
        {
            // Look up product by SKU for the lookup field.
            var query = new QueryExpression("product")
            {
                ColumnSet = new ColumnSet("productid")
            };
            query.Criteria.AddCondition(
                "productnumber", ConditionOperator.Equal, txn.Sku);
            var results = await svc.RetrieveMultipleAsync(query);
            if (results.Entities.Count == 0)
            {
                _logger.LogWarning(
                    "Cannot sync transaction {Id}: product SKU={Sku} not in Dataverse.",
                    txn.TransactionId, txn.Sku);
                return;
            }

            Guid productId = results.Entities[0].Id;

            var transaction = new Entity("cim_stocktransaction");
            transaction["cim_transactionid"] = txn.TransactionId;
            transaction["cim_product"] = new EntityReference("product", productId);
            transaction["cim_transactiontype"] = new OptionSetValue(
                txn.TransactionType == "Inbound" ? 767270000 : 767270001);
            transaction["cim_quantity"] = txn.Quantity;
            transaction["cim_stockbefore"] = txn.StockBefore;
            transaction["cim_stockafter"] = txn.StockAfter;
            transaction["cim_source"] = new OptionSetValue(GetSourceOption(txn.Source));
            transaction["cim_notes"] = txn.Notes;
            transaction["cim_transactiondate"] = txn.TransactionDate;
            transaction["cim_alerttriggered"] = txn.AlertTriggered;

            // Set owner to the requesting user when the email is available.
            if (!string.IsNullOrWhiteSpace(txn.RequestedBy))
            {
                var userQuery = new QueryExpression("systemuser")
                {
                    ColumnSet = new ColumnSet("systemuserid")
                };
                userQuery.Criteria.AddCondition(
                    "internalemailaddress",
                    ConditionOperator.Equal,
                    txn.RequestedBy);

                var users = await svc.RetrieveMultipleAsync(userQuery);
                if (users.Entities.Count > 0)
                {
                    transaction["ownerid"] = new EntityReference(
                        "systemuser", users.Entities[0].Id);
                }
                else
                {
                    _logger.LogWarning(
                        "RequestedBy user not found for transaction {Id}. Email={Email}",
                        txn.TransactionId, txn.RequestedBy);
                }
            }

            await svc.CreateAsync(transaction);

            _logger.LogInformation(
                "Synced transaction {Id} for SKU={Sku}", txn.TransactionId, txn.Sku);
        }

        // ─── Write back with optimistic concurrency ─────────────────────────
        private async Task WriteBackWithRetryAsync(
            InventoryRepository repo, InventoryWorkbook workbook)
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    await repo.WriteAsync(workbook, workbook.ETag);
                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == 412)
                {
                    _logger.LogWarning(
                        "Sync writeback concurrency conflict (attempt {Attempt}/{Max}).",
                        attempt, MaxConcurrencyRetries);

                    if (attempt == MaxConcurrencyRetries)
                    {
                        // Last resort: re-read, merge sync flags, and try once more without precondition.
                        var fresh = await repo.ReadAsync();
                        var syncedIds = workbook.Transactions
                            .Where(t => t.SyncedToDataverse)
                            .Select(t => t.TransactionId)
                            .ToHashSet();

                        foreach (var t in fresh.Transactions)
                            if (syncedIds.Contains(t.TransactionId))
                                t.SyncedToDataverse = true;

                        await repo.WriteAsync(fresh, fresh.ETag);
                        return;
                    }
                    await Task.Delay(200 * attempt);
                }
            }
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private static int GetSourceOption(string source) =>
            (source ?? "").Trim() switch
            {
                "Vendor Delivery" => 767270000,
                "Sales Order" => 767270001,
                "Customer Return" => 767270002,
                "Internal Transfer" => 767270003,
                "Write-off" => 767270004,
                "Manual Adjustment" => 767270005,
                _ => 767270005
            };
    }
}