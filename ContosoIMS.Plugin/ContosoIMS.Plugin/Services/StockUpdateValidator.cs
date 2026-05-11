using ContosoIMS.Plugin.Models;

namespace ContosoIMS.Plugin.Services
{
    /// <inheritdoc />
    public class StockUpdateValidator : IStockUpdateValidator
    {
        public ValidationResult Validate(StockUpdateRequest request)
        {
            if (request == null)
                return ValidationResult.Failure("Validation failed: Request is null.");

            if (string.IsNullOrWhiteSpace(request.sku))
                return ValidationResult.Failure("Validation failed: SKU is required.");

            if (request.quantity <= 0)
                return ValidationResult.Failure("Validation failed: Quantity must be greater than zero.");

            if (string.IsNullOrWhiteSpace(request.source))
                return ValidationResult.Failure("Validation failed: Source is required.");

            return ValidationResult.Success();
        }
    }
}
