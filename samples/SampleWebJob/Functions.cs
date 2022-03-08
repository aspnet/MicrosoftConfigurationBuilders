using Microsoft.Azure.WebJobs;
using System.Collections;
using System.Configuration;
using System.IO;

namespace SampleWebJob
{
    public class Functions
    {
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage([QueueTrigger("queue")] string message, TextWriter log)
        {
            log.WriteLine("===================================================================");
            log.WriteLine("===================================================================");
            log.WriteLine(message);

            log.WriteLine("---------- App Settings ----------");
            foreach (string appsetting in ConfigurationManager.AppSettings.Keys)
            {
                log.WriteLine($"{appsetting}\t{ConfigurationManager.AppSettings[appsetting]}");
            }

            log.WriteLine("");
            log.WriteLine("---------- Connection Strings ----------");
            foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                log.WriteLine($"{cs.Name}\t{cs.ConnectionString}");
            }

            log.WriteLine("");
            log.WriteLine("---------- Environment ----------");
            foreach (DictionaryEntry ev in System.Environment.GetEnvironmentVariables())
            {
                log.WriteLine($"{ev.Key}\t{ev.Value}");
            }
        }
    }
}
