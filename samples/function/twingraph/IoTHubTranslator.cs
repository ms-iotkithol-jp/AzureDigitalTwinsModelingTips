// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.DigitalTwins.Core;
using System.Net.Http;
using System.Collections.Generic;
using Azure.Identity;
using Azure.Core.Pipeline;
using Azure;
using System.Threading.Tasks;

namespace EmbeddedGeorge.ADTSample
{
    public static class IoTHubTranslator
    {
        static DigitalTwinsClient twinsClient = null;
        static HttpClient httpClient = new HttpClient();

        static readonly string  adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        static readonly string tmdModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:TemperatureMeasurementDevice;1";
        static readonly string ccTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:CoolingContainerTruck;1";
        static readonly string dTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:DeliveryTruck;1";
        
        [FunctionName("IoTHubTranslator")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            var exceptions = new List<Exception>();
            try {
                if (twinsClient == null) {
                    if (string.IsNullOrEmpty(adtInstanceUrl)) log.LogInformation("Application setting \"ADT_SERVICE_URL\" not set.");
                    else log.LogInformation($"Got {adtInstanceUrl}");

                    var credential = new ManagedIdentityCredential("https://digitaltwins.azure.net");
                    twinsClient = new DigitalTwinsClient(
                        new Uri(adtInstanceUrl),
                        credential,
                        new DigitalTwinsClientOptions {
                            Transport = new HttpClientTransport(httpClient)
                        }
                    );
                    log.LogInformation($"ADT service client connection created - {adtInstanceUrl}");
                }
                var msg = eventGridEvent.Data.ToString();
                dynamic msgJson = Newtonsoft.Json.JsonConvert.DeserializeObject(msg);
                string deviceId = msgJson["systemProperties"]["iothub-connection-device-id"];
                log.LogInformation($"From {deviceId} - Data:{msg}");
                if (msgJson["properties"]["message-type"] != null) {
                    string msgType = msgJson["properties"]["message-type"];
                    string msgBody = msgJson["body"];
                    string telemetryMsg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(msgBody));
                    dynamic telemetryJson = Newtonsoft.Json.JsonConvert.DeserializeObject(telemetryMsg);
                    if  (msgType == "cctruck") {
                        log.LogInformation($"CCTruck - telemetry message:{telemetryMsg}");
                        string query = $"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND iothub_deviceid = '{deviceId}'";
                        var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                        BasicDigitalTwin ccTruck = null;
                        await foreach(var r in queryResponse) {
                            ccTruck = r;
                            break;
                        }
                        if (ccTruck != null) {
                            // Send Trelemetry
                            var locationJson = await PublishLocationTelemetry(ccTruck, telemetryJson, eventGridEvent.Id, log);
                            dynamic twinTelemetry = Newtonsoft.Json.JsonConvert.DeserializeObject(locationJson);

                            // Update Property
                            var newProps = new Dictionary<string,object>();
                            int ccTruckStatus = (int)telemetryJson["status"];
                            newProps.Add("Status", ccTruckStatus);
                            string location = "";
                            switch (ccTruckStatus) {
                            case 0:
                            case 1:
                            case 3:
                            case 5:
                                location = $"latitude:{twinTelemetry.currentLocation.latitude},longitude:{twinTelemetry.currentLocation.longitude},attitude:{twinTelemetry.currentLocation.attitude}";
                                break;
                            case 2: // AtFactory
                                break;
                            case 4: // AtStation
                                break;
                            }
                            if (!string.IsNullOrEmpty(location)) {
                                newProps.Add("Location", location);
                            }
                            double containerTemp = telemetryJson["container_temperature"];
                            newProps.Add("ContainerTemperature", containerTemp);

                            await UpdateTwinProperties(ccTruck, newProps, log);
                        }
                        else {
                            log.LogWarning("There is no corresponding twin.");
                        }
                    }
                    else if (msgType == "driver-mobile") {
                        log.LogInformation($"DTruck - telemetry message:{telemetryMsg}");
                        string query = $"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{dTruckModelId}') AND iothub_deviceid = '{deviceId}'";
                        var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                        BasicDigitalTwin dTruck = null;
                        await foreach(var r in queryResponse) {
                            dTruck = r;
                            break;
                        }
                        if (dTruck != null) {
                            // Send Trelemetry
                            var locationJson = await PublishLocationTelemetry(dTruck, telemetryJson, eventGridEvent.Id, log);
                            dynamic twinTelemetry = Newtonsoft.Json.JsonConvert.DeserializeObject(locationJson);

                            // Update Property
                            var newProps = new Dictionary<string,object>();
                            int dTruckStatus = (int)telemetryJson["status"];
                            newProps.Add("Status", dTruckStatus);
                            string location = "";
                            if (dTruckStatus == 1 || dTruckStatus == 2 || dTruckStatus == 3) {
                                location = $"latitude:{twinTelemetry.currentLocation.latitude},longitude:{twinTelemetry.currentLocation.longitude},attitude:{twinTelemetry.currentLocation.attitude}";
                            }
                            if (!string.IsNullOrEmpty(location)) {
                                newProps.Add("Location", location);
                            }
                            await UpdateTwinProperties(dTruck, newProps, log);
                            foreach (dynamic tmd in telemetryJson.temperatureMeasurementDevices) {
                                string qTmd = $"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{tmdModelId}') AND $dtId = '{tmd.tmdId}'";
                                var qTmdResponse = twinsClient.QueryAsync<BasicDigitalTwin>(qTmd);
                                BasicDigitalTwin tmdTwin = null;
                                await foreach(var r in qTmdResponse) {
                                    tmdTwin = r;
                                    break;
                                }
                                if (tmdTwin != null) {
                                    log.LogInformation($"tmdTwin.Id={tmdTwin.Id},tmdTwin.ModelId={tmdTwin.Metadata.ModelId}");
                                    var tmdTelemetry = new {
                                        measurement = new {
                                            temperature = (double)tmd.temperature,
                                            batteryLevel = (double)tmd.batteryLevel,
                                            measuredtime = DateTime.Parse((string)tmd.timestamp)
                                        }
                                    };
                                    var tmdTelemetryJson = Newtonsoft.Json.JsonConvert.SerializeObject(tmdTelemetry);
                                    await twinsClient.PublishTelemetryAsync(tmdTwin.Id, eventGridEvent.Id, tmdTelemetryJson);
                                    log.LogInformation($"Published to tmd[{tmdTwin.Id}] - {tmdTelemetryJson}");
                                }
                                else {
                                    log.LogInformation($"There is no coresponded twin for tmd[tmd.tmdId]");
                                }
                            }

                        }
                        else {
                            log.LogWarning("There is no corresponding twin.");
                        }
                    }
                }
                else {
                    log.LogWarning("Message doesn't have 'message-type' property!");
                }
            }
            catch (Exception ex) {
                exceptions.Add(ex);
            }
            foreach (var ex in exceptions) {
                log.LogError(ex.Message);
            }
        }

        private static async Task<string> PublishLocationTelemetry(BasicDigitalTwin truck, dynamic telemetryJson, string messageId, ILogger log)
        {
            dynamic locationJson = telemetryJson["location"];
            var twinTelemetry = new {
                currentLocation = new {
                    longitude = (double)locationJson["longitude"],
                    latitude = (double)locationJson["latitude"],
                    attitude = (double)locationJson["attitude"],
                    measuredtime = DateTime.Parse((string)telemetryJson["timestamp"])
                }
            };
            var twinTelemtryMsg = Newtonsoft.Json.JsonConvert.SerializeObject(twinTelemetry);
            log.LogInformation($"To {truck.Id} - {twinTelemtryMsg}");
            await twinsClient.PublishTelemetryAsync(truck.Id, messageId, twinTelemtryMsg);
            log.LogInformation($"Published to {truck.Id}");

            return Newtonsoft.Json.JsonConvert.SerializeObject(twinTelemetry);
        }

        private static async Task UpdateTwinProperties(BasicDigitalTwin twin, Dictionary<string,object> newValues, ILogger log)
        {
            var patch = new JsonPatchDocument();
            foreach (var prop in newValues.Keys) {
                if (twin.Contents.ContainsKey(prop)) {
                    patch.AppendReplace($"/{prop}", newValues[prop]);
                } else {
                    patch.AppendAdd($"/{prop}", newValues[prop]);
                }
            }
            await twinsClient.UpdateDigitalTwinAsync(twin.Id, patch);
            log.LogInformation($"Updated Twin[{twin.Id}] - '{patch.ToString()}'");
        }
    }
}
