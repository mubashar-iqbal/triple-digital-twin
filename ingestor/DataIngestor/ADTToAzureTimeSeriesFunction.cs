using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataIngestor
{
    //https://docs.microsoft.com/azure/digital-twins/how-to-integrate-time-series-insights?#create-a-function
    
    public static class ADTToAzureTimeSeriesFunction
    {
        [FunctionName("ADTToAzureTimeSeriesFunction")]
        public static async Task Run(
            [EventHubTrigger("ftc-adt-twins-hub", Connection = "EventHubAppSetting-Twins")] EventData myEventHubMessage,
            [EventHub("ftc-adt-time-series-hub", Connection = "EventHubAppSetting-TSI")] IAsyncCollector<string> outputEvents,
            ILogger log)
        {
            JObject message = (JObject)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(myEventHubMessage.Body));
            log.LogInformation($"Reading event: {message}");

            // Read values that are replaced or added
            var tsiUpdate = new Dictionary<string, object>();
            foreach (var operation in message["patch"])
            {
                if (operation["op"].ToString() == "replace" || operation["op"].ToString() == "add")
                {
                    //Convert from JSON patch path to a flattened property for TSI
                    //Example input: /Front/Temperature
                    //        output: Front.Temperature
                    string path = operation["path"].ToString().Substring(1);
                    path = path.Replace("/", ".");
                    tsiUpdate.Add(path, operation["value"]);
                }
            }
            // Send an update if updates exist
            if (tsiUpdate.Count > 0)
            {
                tsiUpdate.Add("$dtId", myEventHubMessage.Properties["cloudEvents:subject"]);
                await outputEvents.AddAsync(JsonConvert.SerializeObject(tsiUpdate));
            }
        }
    }
}
