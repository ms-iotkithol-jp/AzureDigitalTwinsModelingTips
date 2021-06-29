// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmbeddedGeorge.ADTSample
{
    public static class CatchTwinTelemetry
    {
        [FunctionName("CatchTwinTelemetry")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent,
            [EventHub("twintelemetry",Connection="eventhub_twintelemetry_cs")] IAsyncCollector<string> outputEvents, ILogger log)
        {
            var eventGridEventData = eventGridEvent.Data.ToString();
            log.LogInformation($"Id:{eventGridEvent.Id},Subject:{eventGridEvent.Subject} data:{eventGridEventData}");
            log.LogInformation($"Topic:{eventGridEvent.Topic}");
            var exceptions = new List<Exception>();
            try {
                dynamic messageJson = Newtonsoft.Json.JsonConvert.DeserializeObject(eventGridEventData);
                dynamic data = messageJson["data"];
                string dataschema = messageJson["dataschema"];
                string subject = eventGridEvent.Subject;
                if (!string.IsNullOrEmpty(dataschema)) {
                    log.LogInformation($"telemetry - dataschema={dataschema}");
                    var output = new {
                        modelId = dataschema,
                        Id = subject,
                        data = data
                    };
                    var outputJson = Newtonsoft.Json.JsonConvert.SerializeObject(output);
                    await outputEvents.AddAsync(outputJson);
                    log.LogInformation($"Send to event hub - {outputJson}");
                }
            }
            catch (Exception ex) {
                exceptions.Add(ex);
            }
            foreach (var ex in exceptions) {
                log.LogError(ex.Message);
            }

        }
    }
}
