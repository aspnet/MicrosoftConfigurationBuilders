using System.Collections;
using System.Configuration;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace SampleWebJob
{
    public class Functions
    {
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage([QueueTrigger("queue")] string message, ILogger logger)
        {
            StringBuilder log = new StringBuilder();

            log.AppendLine("===================================================================");
            log.AppendLine("===================================================================");
            log.AppendLine(message);

            log.AppendLine("---------- App Settings ----------");
            foreach (string appsetting in ConfigurationManager.AppSettings.Keys)
            {
                log.AppendLine($"{appsetting}\t{ConfigurationManager.AppSettings[appsetting]}");
            }

            log.AppendLine("");
            log.AppendLine("---------- Connection Strings ----------");
            foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                log.AppendLine($"{cs.Name}\t{cs.ConnectionString}");
            }

            log.AppendLine("");
            log.AppendLine("---------- Environment ----------");
            foreach (DictionaryEntry ev in System.Environment.GetEnvironmentVariables())
            {
                log.AppendLine($"{ev.Key}\t{ev.Value}");
            }

            logger.LogInformation(log.ToString());
        }
    }
}
