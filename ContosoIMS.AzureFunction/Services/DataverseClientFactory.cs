using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace ContosoIMS.AzureFunction.Services
{
    /// <summary>
    /// Abstraction over Dataverse <see cref="ServiceClient"/> creation so functions
    /// don't read environment variables or build connection strings inline.
    /// All configuration is sourced from <see cref="IConfiguration"/> (App Settings /
    /// Key Vault references in Azure, local.settings.json in dev).
    /// </summary>
    public interface IDataverseClientFactory
    {
        /// <summary>
        /// Creates a connected <see cref="ServiceClient"/>. Returns <c>null</c>
        /// if configuration is missing or the client could not be initialized.
        /// Caller is responsible for disposing the returned client.
        /// </summary>
        ServiceClient? CreateClient();
    }

    public class DataverseClientFactory : IDataverseClientFactory
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataverseClientFactory> _logger;

        public DataverseClientFactory(
            IConfiguration configuration,
            ILogger<DataverseClientFactory> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public ServiceClient? CreateClient()
        {
            string? url = _configuration["DataverseUrl"];
            string? clientId = _configuration["ClientId"];
            string? clientSecret = _configuration["ClientSecret"];
            string? tenantId = _configuration["TenantId"];
            bool useManagedIdentity = string.Equals(
                _configuration["UseManagedIdentity"], "true",
                StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogError("Dataverse configuration is missing: DataverseUrl is required.");
                return null;
            }

            string connectionString;
            if (useManagedIdentity)
            {
                // Preferred path in Azure: System- or User-Assigned Managed Identity.
                connectionString =
                    $"AuthType=MSI;" +
                    $"Url={url};";
                _logger.LogInformation("Creating Dataverse ServiceClient using Managed Identity.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(clientId) ||
                    string.IsNullOrWhiteSpace(clientSecret) ||
                    string.IsNullOrWhiteSpace(tenantId))
                {
                    _logger.LogError(
                        "Dataverse ClientSecret configuration is incomplete (ClientId/ClientSecret/TenantId).");
                    return null;
                }

                connectionString =
                    $"AuthType=ClientSecret;" +
                    $"Url={url};" +
                    $"ClientId={clientId};" +
                    $"ClientSecret={clientSecret};" +
                    $"TenantId={tenantId};";
                _logger.LogInformation("Creating Dataverse ServiceClient using ClientSecret (App Registration).");
            }

            try
            {
                var svc = new ServiceClient(connectionString);
                if (!svc.IsReady)
                {
                    _logger.LogError("Dataverse ServiceClient not ready: {Error}", svc.LastError);
                    svc.Dispose();
                    return null;
                }
                return svc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to construct Dataverse ServiceClient.");
                return null;
            }
        }
    }
}
