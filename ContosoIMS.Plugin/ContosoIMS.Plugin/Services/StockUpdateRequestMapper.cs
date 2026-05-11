using ContosoIMS.Plugin.Constants;
using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class StockUpdateRequestMapper : IStockUpdateRequestMapper
    {
        public StockUpdateRequest Map(Entity target)
        {
            int transactionTypeValue = EntityAttributeReader.GetOptionSetValue(target, SchemaNames.TransactionType);
            string transactionType = transactionTypeValue == OptionSetValues.TransactionType_Inbound
                ? TransactionTypes.Inbound
                : TransactionTypes.Outbound;

            return new StockUpdateRequest
            {
                sku             = EntityAttributeReader.GetString(target, SchemaNames.Sku),
                quantity        = EntityAttributeReader.GetInteger(target, SchemaNames.Quantity),
                source          = EntityAttributeReader.GetString(target, SchemaNames.Source),
                notes           = EntityAttributeReader.GetString(target, SchemaNames.Notes),
                transactionType = transactionType
            };
        }
    }
}
