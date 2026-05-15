// Models/InventoryItem.cs
namespace ContosoIMS.AzureFunction.Models
{
    /// <summary>
    /// Represents a single row in the "Inventory" sheet of inventory.xlsx.
    /// </summary>
    public class InventoryItem
    {
        public string Sku { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int CurrentStock { get; set; }
        public int ReorderThreshold { get; set; }
        public string StockStatus { get; set; } = "Active";
        public DateTime LastUpdated { get; set; }
        public DateTime? LastRestockedDate { get; set; }
    }

    /// <summary>
    /// Represents a single row in the "Transactions" sheet of inventory.xlsx.
    /// </summary>
    public class InventoryTransaction
    {
        public string TransactionId { get; set; } = "";
        public string Sku { get; set; } = "";
        public string TransactionType { get; set; } = "";
        public int Quantity { get; set; }
        public int StockBefore { get; set; }
        public int StockAfter { get; set; }
        public string RequestedBy { get; set; } = "";
        public string Source { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime TransactionDate { get; set; }
        public bool AlertTriggered { get; set; }
        public bool SyncedToDataverse { get; set; }
    }

    /// <summary>
    /// Container that holds the contents of inventory.xlsx plus the blob ETag
    /// used for optimistic concurrency control on writes.
    /// </summary>
    public class InventoryWorkbook
    {
        public List<InventoryItem> Items { get; set; } = new();
        public List<InventoryTransaction> Transactions { get; set; } = new();
        public Azure.ETag ETag { get; set; }
        public bool BlobExisted { get; set; }
    }
}
