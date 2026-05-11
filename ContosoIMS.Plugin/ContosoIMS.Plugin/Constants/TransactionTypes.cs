namespace ContosoIMS.Plugin.Constants
{
    /// <summary>
    /// String literals for the transaction type field sent to the Azure Function.
    /// Centralized to avoid magic strings (SRP).
    /// </summary>
    internal static class TransactionTypes
    {
        public const string Inbound  = "Inbound";
        public const string Outbound = "Outbound";
    }
}
