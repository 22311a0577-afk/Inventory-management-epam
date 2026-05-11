namespace ContosoIMS.Plugin.Models
{
    /// <summary>
    /// Strongly-typed request payload sent to the Azure Function.
    /// </summary>
    public class StockUpdateRequest
    {
        public string sku { get; set; }
        public string transactionType { get; set; }
        public int quantity { get; set; }
        public string source { get; set; }
        public string notes { get; set; }
    }
}
