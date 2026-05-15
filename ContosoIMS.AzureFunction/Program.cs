using Azure.Storage.Blobs;
using ContosoIMS.AzureFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // BlobServiceClient with built-in retry policy for transient failures.
        // Connection string is read from configuration (App Settings / Key Vault
        // in Azure, local.settings.json in dev). Never hardcoded here.
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            string? conn = config["AzureWebJobsStorage"];
            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException(
                    "AzureWebJobsStorage is not configured. Set it in App Settings or Key Vault.");
            }

            var options = new BlobClientOptions
            {
                Retry =
                {
                    MaxRetries = 5,
                    NetworkTimeout = TimeSpan.FromSeconds(30)
                }
            };
            return new BlobServiceClient(conn, options);
        });

        // Domain services.
        services.AddSingleton<IDataverseClientFactory, DataverseClientFactory>();
        services.AddTransient<InventoryRepository>();
    })
    .Build();

await host.RunAsync();