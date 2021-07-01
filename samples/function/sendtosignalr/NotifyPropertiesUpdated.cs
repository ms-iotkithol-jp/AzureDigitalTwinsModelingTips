using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace EmbeddedGeorge.ADTSample
{
    public static class NotifyPropertiesUpdated
    {
        [FunctionName("NotifyPropertiesUpdated")]
        public static async Task Run([EventHubTrigger("twingraphupdate", Connection = "twinhub_listen_EVENTHUB")] EventData[] events,
            [EventHub("datasource",Connection="signalrhub_send_EVENTHUB")] IAsyncCollector<string> outputEvents, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    await outputEvents.AddAsync(messageBody);
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"Function send to SignalR event hub - {messageBody}");
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
