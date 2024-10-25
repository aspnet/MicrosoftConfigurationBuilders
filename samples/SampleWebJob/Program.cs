using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SampleWebJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?linkid=2250384
    internal class Program
    {
        // Please set AzureWebJobsStorage connection strings in appsettings.json for this WebJob to run.
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .UseEnvironment(EnvironmentName.Development)
                .ConfigureWebJobs(b =>
                {
                    b.AddAzureStorageCoreServices()
                    .AddAzureStorageQueues();
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Information);
                    b.AddConsole();
                });


            var host = builder.Build();
            using (host)
            {
                ILogger logger = host.Services.GetService(typeof(ILogger<Program>)) as ILogger<Program>;

                await host.StartAsync();
                Functions.ProcessQueueMessage("Manual trigger of ProcessMessageQ", logger);
                await host.StopAsync();
            }
        }
    }
}


