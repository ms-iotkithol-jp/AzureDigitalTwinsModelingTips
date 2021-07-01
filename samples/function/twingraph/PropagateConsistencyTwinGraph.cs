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
using System.Threading.Tasks;
using Azure;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace EmbeddedGeorge.ADTSample
{
    public static class PropagateConsistencyTwinGraph
    {
       static DigitalTwinsClient twinsClient = null;
        static HttpClient httpClient = new HttpClient();

        static readonly string  adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        static readonly string tmdModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:TemperatureMeasurementDevice;1";
        static readonly string ccTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:CoolingContainerTruck;1";
        static readonly string dTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:DeliveryTruck;1";
        static readonly string productModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Product;1";
        static readonly string orderModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Order;1";

        [FunctionName("PropagateConsistencyTwinGraph")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent,
            [EventHub("twingraphupdate",Connection="eventhub_twingraph_cs")] IAsyncCollector<string> outputEvents, ILogger log)
        {
            var eventGridEventData = eventGridEvent.Data.ToString();
            log.LogInformation($"Id:{eventGridEvent.Id},Subject:{eventGridEvent.Subject},Topic:{eventGridEvent.Topic},EventType:{eventGridEvent.EventType},data:{eventGridEventData}");
           
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
                dynamic messageJson = Newtonsoft.Json.JsonConvert.DeserializeObject(eventGridEventData);
                dynamic data = messageJson["data"];
                string modelId = data["modelId"];
                string dataschema = messageJson["dataschema"];
                if (eventGridEvent.EventType == "Microsoft.DigitalTwins.Twin.Create" || eventGridEvent.EventType == ":Microsoft.DigitalTwins.Twin.Delete") {
                    var dataJObject = (JObject)eventGridEvent.Data;
                    var dataContent =(JObject)dataJObject.GetValue("data");
                    var dtId = dataContent.GetValue("$dtId").ToString();
                    var metadata = (JObject)dataContent.GetValue("$metadata");
                    var model = metadata.GetValue("$model").ToString();
                    object msg = null;
                    if (model == orderModelId) {
                        msg = new {
                            eventtype = eventGridEvent.EventType,
                            modelId = model,
                            Id = dtId
                        };
                    }
                    if (model == productModelId || model == orderModelId) {
                        string orderId = null;
                        var orderRels = twinsClient.GetIncomingRelationshipsAsync(dtId);
                        await foreach (var orderRel in orderRels) {
                            if (orderRel.RelationshipName == "created") {
                                orderId = orderRel.SourceId;
                            }
                        }
                        msg = new {
                            eventtype = eventGridEvent.EventType,
                            modelId = model,
                            Id = dtId,
                            orderId = orderId
                        };
                    }
                    var msgJson = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                    await outputEvents.AddAsync(msgJson);
                    log.LogInformation($"Send to event hub - {msgJson}");
                }
                else if (eventGridEvent.EventType == "Microsoft.DigitalTwins.Twin.Update") {
                    if (!string.IsNullOrEmpty(modelId)) {
                        log.LogInformation($"modelId:{modelId}");
                        // Truck.Location -> Product.Location
                        // CoolingContainerTruck.ContainerTemperature -> Product.CurrentTemperature
                        // CoolingContainerTruck.Status=>AtStation -> Product.Location
                        // CoolingContainerTruck.Status, DeliveryTruck.Status -> Order.Status
                        // TemperatureMeasurementDevice.temperature -> Product.CurrentTemperature
                        // Product.CurrentTemperature,LowAllowableTemperature,HighAllowableTemperature -> Product.Status
                        if (modelId == ccTruckModelId) {
                            string ccTruckId = eventGridEvent.Subject;
                            var carryingProducts = new List<string>();
                            var productRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(ccTruckId, "carrying");
                            await foreach (var productRel in productRels) {
                                carryingProducts.Add(productRel.TargetId);
                            }
                            var updateProps = new Dictionary<string, object>();
                            foreach(dynamic patch in data["patch"]) {
                                string patchPath = patch["path"];
                                string patchOp = patch["op"];
                                if (patchPath == "/Location" && patchOp == "replace") {
                                    foreach (var productId in carryingProducts) {
                                        updateProps.Add(patchPath.Substring(1), (string)patch["value"]);
                                    }
                                    var outputMsg = new {
                                        modelId = ccTruckModelId,
                                        Id = ccTruckId,
                                        Location = (string)patch["value"]
                                    };
                                    await outputEvents.AddAsync(Newtonsoft.Json.JsonConvert.SerializeObject(outputMsg));
                                } else if (patchPath == "/ContainerTemperature") {
                                    double temp = patch["value"];
                                    updateProps.Add("CurrentTemperature", temp);
                                }
                                else if (patchPath == "/Status" && patchOp == "replace") {
                                    int ccTruckStatus = patch["value"];
                                    string orderStatus = "";
                                    switch (ccTruckStatus) {
                                    case 1: // DriveToStation
                                        orderStatus = "TransportingToStation";
                                        break;
                                    case 2: // AtStation
                                        orderStatus = "PreparingAtStation";
                                        break;
                                    }
                                    if (!string.IsNullOrEmpty(orderStatus)) {
                                        foreach (var productId in carryingProducts) {
                                            var outputJson = await UpdateOrderStatus(productId, orderStatus, log);
                                            log.LogInformation($"Sent to eventhub - {outputJson}");
                                            if (!string.IsNullOrEmpty(outputJson)) {
                                                await outputEvents.AddAsync(outputJson);
                                            }
                                        }
                                    }
                                    var outputMsg = new {
                                        modelId = ccTruckModelId,
                                        Id = ccTruckId,
                                        Status = ccTruckStatus
                                    };
                                    await outputEvents.AddAsync(Newtonsoft.Json.JsonConvert.SerializeObject(outputMsg));
                                }
                            }
                            foreach (var productId in carryingProducts) {
                                var productInfo = await UpdateProductProperties(productId, updateProps, log);
                                string outputJson = Newtonsoft.Json.JsonConvert.SerializeObject(productInfo);
                                await outputEvents.AddAsync(outputJson);
                                log.LogInformation($"Sent to event hub - {outputJson}");
                            }
                        } else if (modelId == dTruckModelId) {
                            string dTruckId = eventGridEvent.Subject;
                            var carryingProducts = new List<string>();
                            var productRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(dTruckId, "carrying");
                            await foreach (var productRel in productRels) {
                                carryingProducts.Add(productRel.TargetId);
                            }
                            foreach(dynamic patch in data["patch"]) {
                                string patchPath = patch["path"];
                                string patchOp = patch["op"];
                                if (patchPath == "/Location" && patchOp == "replace") {
                                    var outputMsg = new {
                                        modelId = dTruckModelId,
                                        Id = dTruckId,
                                        Location = (string)patch["value"]
                                    };
                                    await outputEvents.AddAsync(Newtonsoft.Json.JsonConvert.SerializeObject(outputMsg));
                                }
                                else if (patchPath == "/Status" && patchOp == "replace") {
                                    int dTruckStatus = patch["value"];
                                    string orderStatus = "";
                                    switch (dTruckStatus) {
                                    case 1: // DeliveringToCustomers
                                        orderStatus = "DeliveringToCustomer";
                                        break;
                                    }
                                    if (!string.IsNullOrEmpty(orderStatus)) {
                                        foreach (var productId in carryingProducts) {
                                            var outputJson = await UpdateOrderStatus(productId, orderStatus, log);
                                            if (!string.IsNullOrEmpty(outputJson)) {
                                                await outputEvents.AddAsync(outputJson);
                                                log.LogInformation($"Sent to eventhub - {outputJson}");
                                            }
                                        }
                                    }
                                    var outputMsg = new {
                                        modelId = dTruckModelId,
                                        Id = dTruckId,
                                        TaskStatus = dTruckStatus
                                    };
                                    await outputEvents.AddAsync(Newtonsoft.Json.JsonConvert.SerializeObject(outputMsg));
                                }
                            }
                        }
                    } else {
                        log.LogInformation("Do anything?");
                    }
                } else if (eventGridEvent.EventType == "microsoft.iot.telemetry") {
                    if (!string.IsNullOrEmpty(dataschema)) {
                        log.LogInformation($"dataschema:{dataschema}");
                        // TemperatureMeasurementDevice's telemetry measurement.temperature -> Product.CurrentTemperature
                        if (dataschema == tmdModelId) {
                            string tmdId = eventGridEvent.Subject;
                            var productRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(tmdId, "target");
                            string productId = "";
                            await foreach (var productRel in productRels) {
                                productId = productRel.TargetId;
                                break;
                            }
                            BasicDigitalTwin dTruckTwin = null;
                            var dTruckRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(tmdId, "assigned_to");
                            await foreach (var dTruckRel in dTruckRels) {
                                var dTruckId = dTruckRel.TargetId;
                                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{dTruckModelId}') AND $dtId='{dTruckId}'";
                                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                                await foreach (var t in queryResponse) {
                                    dTruckTwin = t;
                                    break;
                                }
                                break;
                            }
                            if (!string.IsNullOrEmpty(productId) && dTruckTwin != null) {
                                dynamic measurement = data["measurement"];
                                double temp = measurement["temperature"];
                                log.LogInformation($"target - Product[{productId}] <- temperature={temp}");
                                var updateTwin = new Dictionary<string,object>();
                                updateTwin.Add("CurrentTemperature", temp);
                                var productInfo = await UpdateProductProperties(productId, updateTwin, log);
                                if (dTruckTwin.Contents.ContainsKey("Location")) {
                                    string truckLocation = ((JsonElement)dTruckTwin.Contents["Location"]).GetString();
                                    productInfo.Location = truckLocation;
                                    log.LogInformation($"location <= {truckLocation}");
                                }
                                string outputJson = Newtonsoft.Json.JsonConvert.SerializeObject(productInfo);
                                await outputEvents.AddAsync(outputJson);
                                log.LogInformation($"Sent to event hub - {outputJson}");
                            }
                        }
                    }
                } else {
                    log.LogInformation($"Current logic do nothing for {eventGridEvent.EventType}");
                }
            }
            catch (Exception ex) {
                exceptions.Add(ex);
            }
            foreach (var ex in exceptions) {
                log.LogError(ex.Message);
            }
        }

        static async Task<ProductInfo> UpdateProductProperties(string productId, Dictionary<string, object> newPropValues, ILogger log)
        {
            var productInfo = new ProductInfo() {
                modelId = productModelId,
                Id = productId
            };
            var updateTwin = new JsonPatchDocument();
            foreach(var newPVKey in newPropValues.Keys) {
                if (newPropValues[newPVKey]==null) continue;
                if (newPVKey == "CurrentTemperature") {
                    var nextStatus = await GetProductNextStatus(productId, (double)newPropValues[newPVKey], log);
                    if (nextStatus != -1) {
                        updateTwin.AppendReplace("/Status", nextStatus);
                        productInfo.Status = nextStatus;
                    }
                    productInfo.Temperature = (double)newPropValues[newPVKey];
                } else {
                    updateTwin.AppendReplace($"/{newPVKey}", newPropValues[newPVKey]);
                    if (newPVKey == "Location") {
                        productInfo.Location = (string)newPropValues[newPVKey];
                    }
                }
                log.LogInformation($"!!Update [{newPVKey}]<={newPropValues[newPVKey]}");
            }
            await twinsClient.UpdateDigitalTwinAsync(productId, updateTwin);
            log.LogInformation($"!!Update Product[{productId}]");

            return productInfo;
        }

        static async Task<int> GetProductNextStatus(string productId, double currentTemperature, ILogger log) {
            var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{productModelId}') AND $dtId='{productId}'";
            var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            BasicDigitalTwin productTwin = null;
            await foreach (var p in queryResponse) {
                productTwin = p;
                break;
            }
            log.LogInformation($"Find Product[{productTwin.Id}]");
            int nextStatus = -1;
            if (productTwin.Contents.ContainsKey("LowAllowableTemperature") && productTwin.Contents.ContainsKey("HighAllowableTemperature")) {
                double lowLimit = ((JsonElement)productTwin.Contents["LowAllowableTemperature"]).GetDouble();
                double highLimit = ((JsonElement)productTwin.Contents["HighAllowableTemperature"]).GetDouble();
                log.LogInformation($"Product - lowLimit={lowLimit},highLimit={highLimit}");
                if (currentTemperature < lowLimit || highLimit < currentTemperature) {
                    nextStatus = 3;
                } else {
                    double allowableRange = highLimit - lowLimit;
                    if (allowableRange != 0.0) {
                        if ((highLimit - currentTemperature) / allowableRange < 0.1) {
                            nextStatus = 2;
                        } else {
                            if ((currentTemperature - lowLimit) / allowableRange < 0.1) {
                                nextStatus = 1;
                            }
                        }
                    }
                }
            }
            int currentStatus = ((JsonElement)productTwin.Contents["Status"]).GetInt32();
            if (nextStatus != -1 && currentStatus == nextStatus) {
                nextStatus = -1;
            }
            return nextStatus;
        }

        static async Task<string> UpdateOrderStatus(string productId, string newStatus, ILogger log)
        {
            string resultJson = "";
            string orderId = "";
            var orderRels = twinsClient.GetIncomingRelationshipsAsync(productId);
            await foreach(var orderRel in orderRels) {
                if (orderRel.RelationshipName == "created") {
                    orderId = orderRel.SourceId;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(orderId)) {
                var updateTwin = new JsonPatchDocument();
                updateTwin.AppendReplace("/Status", newStatus);
                await twinsClient.UpdateDigitalTwinAsync(orderId, updateTwin);
                var outputMsg = new {
                    modelId = orderModelId,
                    Id = orderId,
                    Status = newStatus
                };
                resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(outputMsg);
            } else {
                log.LogWarning($"There is no Order for Product[{productId}]");
            }

            return resultJson;
        }
    }

    class ProductInfo {
        public string modelId { get; set; }
        public string Id { get; set; }
        public string Location { get; set; }
        public double Temperature { get; set; }
        public int Status { get; set; }
    }
}
