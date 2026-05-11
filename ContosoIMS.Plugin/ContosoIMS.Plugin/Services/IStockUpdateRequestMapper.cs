using ContosoIMS.Plugin.Models;
using Microsoft.Xrm.Sdk;

namespace ContosoIMS.Plugin.Services
{
    /// <summary>Maps a Dataverse Target entity into a typed Azure Function request.</summary>
    public interface IStockUpdateRequestMapper
    {
        StockUpdateRequest Map(Entity target);
    }
}
