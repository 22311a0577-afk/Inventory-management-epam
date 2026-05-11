namespace ContosoIMS.Plugin.Models
{
    /// <summary>
    /// Response contract returned by the downstream Azure Function.
    /// </summary>
    public class AzureFunctionResponse
    {
        public bool Success { get; set; }
        public int NewStockLevel { get; set; }
        public bool AlertTriggered { get; set; }
        public string TransactionId { get; set; }
        public string Message { get; set; }
    }
}
