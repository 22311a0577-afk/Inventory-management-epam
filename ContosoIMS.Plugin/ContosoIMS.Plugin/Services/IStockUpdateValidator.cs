using ContosoIMS.Plugin.Models;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Validates a <see cref="StockUpdateRequest"/> against business rules.</summary>
    public interface IStockUpdateValidator
    {
        ValidationResult Validate(StockUpdateRequest request);
    }
}
