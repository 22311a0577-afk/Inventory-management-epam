namespace ContosoIMS.Plugin.Models
{
    public class StockUpdateRequest
    {
        public string sku { get; set; }
        public string transactionType { get; set; }
        public int quantity { get; set; }
        public string source { get; set; }
        public string notes { get; set; }
        public string requestedBy { get; set; }   // NEW — initiating user's email
    }
}