using System;
using Microsoft.Azure.WebJobs;

namespace SampleWebJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    internal class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            // The following code ensures that the WebJob will be running continuously
            //var host = new JobHost(config);
            //host.RunAndBlock();
            Functions.ProcessQueueMessage("Manual trigger of ProcessMessageQ", Console.Out);
        }
    }
}
