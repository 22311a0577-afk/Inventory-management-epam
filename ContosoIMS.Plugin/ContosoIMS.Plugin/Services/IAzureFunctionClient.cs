using ContosoIMS.Plugin.Models;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Calls the downstream Azure Function. Hides all HTTP plumbing (DIP / SRP).</summary>
    public interface IAzureFunctionClient
    {
        AzureFunctionResponse Send(StockUpdateRequest request);
    }
}
