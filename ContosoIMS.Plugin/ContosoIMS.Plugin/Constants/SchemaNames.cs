namespace ContosoIMS.Plugin.Constants
{
    /// <summary>
    /// Centralized Dataverse schema names. Eliminates magic strings (SRP).
    /// </summary>
    internal static class SchemaNames
    {
        public const string StockUpdateRequestEntity = "cim_stockupdaterequest";

        public const string Sku             = "cim_sku";
        public const string Quantity        = "cim_quantity";
        public const string Source          = "cim_source";
        public const string Notes           = "cim_notes";
        public const string TransactionType = "cim_transactiontype";

        public const string Status          = "cim_status";
        public const string NewStockLevel   = "cim_newstocklevel";
        public const string AlertTriggered  = "cim_alerttriggered";
        public const string TransactionId   = "cim_transactionid";
        public const string ResponseMessage = "cim_responsemessage";

        public const string MessageCreate = "Create";
        public const string TargetKey     = "Target";
        public const string IdOutputKey   = "id";
    }
}
