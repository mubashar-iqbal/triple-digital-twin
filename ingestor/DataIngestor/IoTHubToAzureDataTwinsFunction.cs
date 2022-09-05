// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Azure.Identity;
using Azure.DigitalTwins.Core;
using Azure.Core.Pipeline;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Azure;
using System.Threading.Tasks;

namespace DataIngestor
{
    //https://docs.microsoft.com/en-us/azure/digital-twins/how-to-authenticate-client#write-application-code
    //https://docs.microsoft.com/en-us/azure/digital-twins/how-to-ingest-iot-hub-data?tabs=cli#create-a-function

    public static class IoTHubToAzureDataTwinsFunction
    {
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("IoTHubToADTFunction")]
        public static async Task RunAsync([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());

            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");

            try
            {
                // Authenticate with Digital Twins
                var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net");
                var client = new DigitalTwinsClient(
                    new Uri(adtInstanceUrl),
                    cred,
                    new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"ADT service client connection created.");

                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // <Find_device_ID_and_temperature>
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    
                    // log.LogInformation($"Device:{deviceMessage}");
                    
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    var temperature = deviceMessage["body"]["temperature"];
                    var temperatureAlert = deviceMessage["body"]["temperatureAlert"];
                    
                    // log.LogInformation($"Device:{deviceMessage}");
                    log.LogInformation($"Device:{deviceId} Temperature is:{temperature}, Temp Alert is: {temperatureAlert}");

                    // <Update_twin_with_device_temperature>
                    // https://docs.microsoft.com/en-us/azure/digital-twins/how-to-manage-twin
                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendAdd("/temperature", temperature.Value<double>());
                    updateTwinData.AppendAdd("/temperatureAlert", temperatureAlert.Value<bool>());
                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error in ingest function: {ex.Message}");
            }
        }
    }
}
