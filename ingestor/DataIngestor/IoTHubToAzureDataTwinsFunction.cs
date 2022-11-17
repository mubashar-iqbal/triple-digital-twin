using System;
using Microsoft.Azure.WebJobs;
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
using Nethereum.Web3;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace DataIngestor
{

    [FunctionOutput]
    public class GetTemperatureOutputDTO: IFunctionOutputDTO 
    {
        [Parameter("int256", "minTemp", 1)]
        public virtual BigInteger MinTemp { get; set; }
        [Parameter("int256", "maxTemp", 2)]
        public virtual BigInteger MaxTemp { get; set; }
    }
    
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

                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    double temperature = (double)deviceMessage["body"]["temperature"];
                    string temperatureAlert = "Low";
                    string deviceState = "Funcational";

                    var web3 = new Web3 ("https://goerli.infura.io/v3/87931b7ff16a4621b47579bc854d8122");
                    
                    var ABI = @"[{'inputs':[],'name':'GetTemperatureThreshold','outputs':[{'internalType':'int256','name':'','type':'int256'},{'internalType':'int256','name':'','type':'int256'}],'stateMutability':'view','type':'function'},{'inputs':[{'internalType':'int256','name':'_minThreshold','type':'int256'},{'internalType':'int256','name':'_maxThreshold','type':'int256'}],'name':'SetTemperatureThreshold','outputs':[],'stateMutability':'nonpayable','type':'function'}]";

                    var contract = web3.Eth.GetContract(ABI, "0x4F88a5f5d0218CcEC426a390FC5b230409ab9869");

                    var temperatureThreshold = contract.GetFunction("GetTemperatureThreshold");
                    var thresholdValues = await temperatureThreshold.CallDeserializingToObjectAsync<GetTemperatureOutputDTO>();

                    log.LogInformation($"Min temp: {thresholdValues.MinTemp}");
                    log.LogInformation($"Max temp: {thresholdValues.MaxTemp}");

                    double temperatureMaxthreshold = (double)(thresholdValues.MinTemp > 0 ? thresholdValues.MinTemp : 1);
                    double temperatureMinthreshold = (double)(thresholdValues.MaxTemp > 0 ? thresholdValues.MaxTemp : 1);

                    if (temperature > temperatureMaxthreshold)
                    {
                        temperatureAlert = "High";
                    }
                    else if (temperature <= temperatureMaxthreshold && temperature > temperatureMinthreshold+(temperatureMinthreshold/2))
                    {
                        temperatureAlert = "Medium";
                    }
                    else if (temperature < temperatureMinthreshold+(temperatureMinthreshold/2) && temperature >= temperatureMinthreshold)
                    {
                        temperatureAlert = "Normal";
                    }

                    if (temperature < temperatureMinthreshold)
                    {
                        deviceState = "Non-functional";
                    }

                    log.LogInformation($"Device: {deviceId} Temperature is: {temperature}, Temp Alert is: {temperatureAlert}, Device state is: {deviceState}");

                    // https://docs.microsoft.com/en-us/azure/digital-twins/how-to-manage-twin
                    
                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendAdd("/Id", deviceId);
                    updateTwinData.AppendAdd("/Temperature", temperature);
                    updateTwinData.AppendAdd("/TemperatureAlert", temperatureAlert);
                    updateTwinData.AppendAdd("/DeviceState", deviceState);
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
