using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ClosedXML.Excel;
using ContosoIMS.AzureFunction.Models;
using Microsoft.Extensions.Logging;

namespace ContosoIMS.AzureFunction.Services
{
    /// <summary>
    /// Reads and writes inventory.xlsx (with Inventory + Transactions sheets)
    /// to Azure Blob Storage. Uses ETag-based optimistic concurrency on writes
    /// so concurrent edits (user via Excel vs. HTTP function) cannot silently
    /// overwrite each other.
    /// </summary>
    public class InventoryRepository
    {
        public const string ContainerName = "inventory";
        public const string BlobName = "inventory.xlsx";
        public const string InventorySheet = "Inventory";
        public const string TransactionsSheet = "Transactions";

        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<InventoryRepository> _logger;

        public InventoryRepository(
            BlobServiceClient blobServiceClient,
            ILogger<InventoryRepository> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        // ─── Read ──────────────────────────────────────────────────────────
        public async Task<InventoryWorkbook> ReadAsync()
        {
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await container.CreateIfNotExistsAsync();
            var blobClient = container.GetBlobClient(BlobName);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("{Blob} not found — returning empty workbook.", BlobName);
                return new InventoryWorkbook { BlobExisted = false };
            }

            var download = await blobClient.DownloadContentAsync();
            using var ms = new MemoryStream(download.Value.Content.ToArray());
            return ParseWorkbook(ms, download.Value.Details.ETag);
        }

        public InventoryWorkbook ParseWorkbook(Stream stream, ETag etag)
        {
            using var workbook = new XLWorkbook(stream);
            var result = new InventoryWorkbook
            {
                ETag = etag,
                BlobExisted = true
            };

            if (workbook.TryGetWorksheet(InventorySheet, out var invSheet))
            {
                result.Items = ReadInventory(invSheet);
            }

            if (workbook.TryGetWorksheet(TransactionsSheet, out var txnSheet))
            {
                result.Transactions = ReadTransactions(txnSheet);
            }

            return result;
        }

        private List<InventoryItem> ReadInventory(IXLWorksheet sheet)
        {
            var items = new List<InventoryItem>();

            // Header on row 1; data starts row 2
            int row = 2;
            while (!sheet.Row(row).IsEmpty())
            {
                try
                {
                    var item = new InventoryItem
                    {
                        Sku = sheet.Cell(row, 1).GetString().Trim(),
                        ProductName = sheet.Cell(row, 2).GetString().Trim(),
                        CurrentStock = (int)sheet.Cell(row, 3).GetDouble(),
                        ReorderThreshold = (int)sheet.Cell(row, 4).GetDouble(),
                        StockStatus = sheet.Cell(row, 5).GetString().Trim(),
                        LastUpdated = sheet.Cell(row, 6).GetDateTime(),
                        LastRestockedDate = sheet.Cell(row, 7).IsEmpty()
                                                ? null
                                                : sheet.Cell(row, 7).GetDateTime()
                    };

                    if (string.IsNullOrEmpty(item.Sku))
                    {
                        _logger.LogWarning("Skipping inventory row {Row}: empty SKU", row);
                    }
                    else
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Skipping invalid inventory row {Row}: {Error}", row, ex.Message);
                }

                row++;
            }

            return items;
        }

        private List<InventoryTransaction> ReadTransactions(IXLWorksheet sheet)
        {
            var txns = new List<InventoryTransaction>();

            int row = 2;
            while (!sheet.Row(row).IsEmpty())
            {
                try
                {
                    var txn = new InventoryTransaction
                    {
                        TransactionId = sheet.Cell(row, 1).GetString().Trim(),
                        Sku = sheet.Cell(row, 2).GetString().Trim(),
                        TransactionType = sheet.Cell(row, 3).GetString().Trim(),
                        Quantity = (int)sheet.Cell(row, 4).GetDouble(),
                        StockBefore = (int)sheet.Cell(row, 5).GetDouble(),
                        StockAfter = (int)sheet.Cell(row, 6).GetDouble(),
                        Source = sheet.Cell(row, 7).GetString().Trim(),
                        Notes = sheet.Cell(row, 8).GetString().Trim(),
                        TransactionDate = sheet.Cell(row, 9).GetDateTime(),
                        AlertTriggered = ParseBool(sheet.Cell(row, 10).GetString()),
                        SyncedToDataverse = ParseBool(sheet.Cell(row, 11).GetString()),
                        RequestedBy = sheet.Cell(row, 12).GetString().Trim()
                    };

                    if (!string.IsNullOrEmpty(txn.TransactionId))
                        txns.Add(txn);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Skipping invalid transaction row {Row}: {Error}", row, ex.Message);
                }

                row++;
            }

            return txns;
        }

        private static bool ParseBool(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || s == "1";
        }

        // ─── Write (with optimistic concurrency) ────────────────────────────
        /// <summary>
        /// Writes the workbook back to the blob. If <paramref name="etag"/> is
        /// not <c>default</c>, an If-Match header is used so the upload fails
        /// (412 Precondition Failed) if the blob has been modified concurrently.
        /// Returns the new ETag.
        /// </summary>
        public async Task<ETag> WriteAsync(InventoryWorkbook data, ETag etag)
        {
            var container = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await container.CreateIfNotExistsAsync();
            var blobClient = container.GetBlobClient(BlobName);

            using var ms = new MemoryStream();
            BuildWorkbook(data).SaveAs(ms);
            ms.Position = 0;

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType =
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                }
            };

            // Optimistic concurrency: only overwrite if ETag matches.
            // If etag == default => first write (no precondition).
            if (etag != default)
                options.Conditions = new BlobRequestConditions { IfMatch = etag };

            var resp = await blobClient.UploadAsync(ms, options);
            return resp.Value.ETag;
        }

        private static XLWorkbook BuildWorkbook(InventoryWorkbook data)
        {
            var wb = new XLWorkbook();

            // ─── Inventory sheet ─────────────────────────────
            var inv = wb.AddWorksheet(InventorySheet);
            inv.Cell(1, 1).Value = "Sku";
            inv.Cell(1, 2).Value = "ProductName";
            inv.Cell(1, 3).Value = "CurrentStock";
            inv.Cell(1, 4).Value = "ReorderThreshold";
            inv.Cell(1, 5).Value = "StockStatus";
            inv.Cell(1, 6).Value = "LastUpdated";
            inv.Cell(1, 7).Value = "LastRestockedDate";
            inv.Range(1, 1, 1, 7).Style.Font.Bold = true;

            for (int i = 0; i < data.Items.Count; i++)
            {
                int row = i + 2;
                var item = data.Items[i];
                inv.Cell(row, 1).Value = item.Sku;
                inv.Cell(row, 2).Value = item.ProductName;
                inv.Cell(row, 3).Value = item.CurrentStock;
                inv.Cell(row, 4).Value = item.ReorderThreshold;
                inv.Cell(row, 5).Value = item.StockStatus;
                inv.Cell(row, 6).Value = item.LastUpdated;
                inv.Cell(row, 6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                if (item.LastRestockedDate.HasValue)
                {
                    inv.Cell(row, 7).Value = item.LastRestockedDate.Value;
                    inv.Cell(row, 7).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                }
            }
            inv.Columns().AdjustToContents();

            // ─── Transactions sheet (protected — function-managed) ──────────
            var txn = wb.AddWorksheet(TransactionsSheet);
            txn.Cell(1, 1).Value = "TransactionId";
            txn.Cell(1, 2).Value = "Sku";
            txn.Cell(1, 3).Value = "TransactionType";
            txn.Cell(1, 4).Value = "Quantity";
            txn.Cell(1, 5).Value = "StockBefore";
            txn.Cell(1, 6).Value = "StockAfter";
            txn.Cell(1, 7).Value = "Source";
            txn.Cell(1, 8).Value = "Notes";
            txn.Cell(1, 9).Value = "TransactionDate";
            txn.Cell(1, 10).Value = "AlertTriggered";
            txn.Cell(1, 11).Value = "SyncedToDataverse";
            txn.Cell(1, 12).Value = "RequestedBy";
            txn.Range(1, 1, 1, 12).Style.Font.Bold = true;

            for (int i = 0; i < data.Transactions.Count; i++)
            {
                int row = i + 2;
                var t = data.Transactions[i];
                txn.Cell(row, 1).Value = t.TransactionId;
                txn.Cell(row, 2).Value = t.Sku;
                txn.Cell(row, 3).Value = t.TransactionType;
                txn.Cell(row, 4).Value = t.Quantity;
                txn.Cell(row, 5).Value = t.StockBefore;
                txn.Cell(row, 6).Value = t.StockAfter;
                txn.Cell(row, 7).Value = t.Source;
                txn.Cell(row, 8).Value = t.Notes;
                txn.Cell(row, 9).Value = t.TransactionDate;
                txn.Cell(row, 9).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                txn.Cell(row, 10).Value = t.AlertTriggered;
                txn.Cell(row, 11).Value = t.SyncedToDataverse;
                txn.Cell(row, 12).Value = t.RequestedBy;
            }
            txn.Columns().AdjustToContents();

            // Lock the Transactions sheet — users shouldn't edit audit data.
            txn.Protect("ContosoIMS-Audit").AllowedElements = XLSheetProtectionElements.SelectEverything;

            return wb;
        }
    }
}
